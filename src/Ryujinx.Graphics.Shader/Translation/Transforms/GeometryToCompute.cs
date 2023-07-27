using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.Translation.Optimizations;
using System.Collections.Generic;

using static Ryujinx.Graphics.Shader.IntermediateRepresentation.OperandHelper;

namespace Ryujinx.Graphics.Shader.Translation.Transforms
{
    class GeometryToCompute : ITransformPass
    {
        public static bool IsEnabled(IGpuAccessor gpuAccessor, ShaderStage stage, TargetLanguage targetLanguage, FeatureFlags usedFeatures)
        {
            return usedFeatures.HasFlag(FeatureFlags.VtgAsCompute);
        }

        public static LinkedListNode<INode> RunPass(
            HelperFunctionManager hfm,
            LinkedListNode<INode> node,
            ShaderDefinitions definitions,
            ResourceManager resourceManager,
            IGpuAccessor gpuAccessor,
            ShaderStage stage,
            ref FeatureFlags usedFeatures)
        {
            if (definitions.OriginalStage != ShaderStage.Geometry)
            {
                return node;
            }

            Operation operation = (Operation)node.Value;

            LinkedListNode<INode> newNode = node;

            switch (operation.Inst)
            {
                case Instruction.EmitVertex:
                    newNode = GenerateEmitVertex(definitions, resourceManager, node);
                    break;
                case Instruction.EndPrimitive:
                    newNode = GenerateEndPrimitive(definitions, resourceManager, node);
                    break;
                case Instruction.Load:
                    if (operation.StorageKind == StorageKind.Input)
                    {
                        IoVariable ioVariable = (IoVariable)operation.GetSource(0).Value;

                        if (TryGetOffset(resourceManager, operation, StorageKind.Input, out int inputOffset))
                        {
                            int verticesPerPrimitive = definitions.InputTopology.ToInputVertices();

                            Operand primVertex = ioVariable == IoVariable.UserDefined
                                ? operation.GetSource(2)
                                : operation.GetSource(1);

                            Operand vertexElemOffset = GenerateVertexOffset(
                                resourceManager,
                                node,
                                verticesPerPrimitive,
                                inputOffset,
                                primVertex);

                            newNode = node.List.AddBefore(node, new Operation(
                                Instruction.Load,
                                StorageKind.StorageBuffer,
                                operation.Dest,
                                new[] { Const(resourceManager.Reservations.GetVertexOutputStorageBufferBinding()), Const(0), vertexElemOffset }));
                        }
                        else
                        {
                            switch (ioVariable)
                            {
                                case IoVariable.PrimitiveId:
                                    newNode = GeneratePrimitiveId(resourceManager, node, operation.Dest);
                                    break;
                                case IoVariable.GlobalInvocationId:
                                case IoVariable.SubgroupEqMask:
                                case IoVariable.SubgroupGeMask:
                                case IoVariable.SubgroupGtMask:
                                case IoVariable.SubgroupLaneId:
                                case IoVariable.SubgroupLeMask:
                                case IoVariable.SubgroupLtMask:
                                    // Those are valid or expected for geometry shaders.
                                    break;
                                default:
                                    gpuAccessor.Log($"Invalid input \"{ioVariable}\".");
                                    break;
                            }
                        }
                    }
                    else if (operation.StorageKind == StorageKind.Output)
                    {
                        if (TryGetOffset(resourceManager, operation, StorageKind.Output, out int outputOffset))
                        {
                            newNode = node.List.AddBefore(node, new Operation(
                                Instruction.Load,
                                StorageKind.LocalMemory,
                                operation.Dest,
                                new[] { Const(resourceManager.LocalVertexDataMemoryId), Const(outputOffset) }));
                        }
                        else
                        {
                            gpuAccessor.Log($"Invalid output \"{(IoVariable)operation.GetSource(0).Value}\".");
                        }
                    }
                    break;
                case Instruction.Store:
                    if (operation.StorageKind == StorageKind.Output)
                    {
                        if (TryGetOffset(resourceManager, operation, StorageKind.Output, out int outputOffset))
                        {
                            Operand value = operation.GetSource(operation.SourcesCount - 1);

                            newNode = node.List.AddBefore(node, new Operation(
                                Instruction.Store,
                                StorageKind.LocalMemory,
                                (Operand)null,
                                new[] { Const(resourceManager.LocalVertexDataMemoryId), Const(outputOffset), value }));
                        }
                        else
                        {
                            gpuAccessor.Log($"Invalid output \"{(IoVariable)operation.GetSource(0).Value}\".");
                        }
                    }
                    break;
            }

            if (newNode != node)
            {
                Utils.DeleteNode(node, operation);
            }

            return newNode;
        }

        private static LinkedListNode<INode> GenerateEmitVertex(ShaderDefinitions definitions, ResourceManager resourceManager, LinkedListNode<INode> node)
        {
            int vbOutputBinding = resourceManager.Reservations.GetGeometryVertexOutputStorageBufferBinding();
            int ibOutputBinding = resourceManager.Reservations.GetGeometryIndexOutputStorageBufferBinding();
            int stride = resourceManager.Reservations.OutputSizePerInvocation;

            Operand outputPrimVertex = IncrementLocalMemory(node, resourceManager.LocalGeometryOutputVertexCountMemoryId);
            Operand baseVertexOffset = GenerateBaseOffset(resourceManager, node, definitions.MaxOutputVertices);
            Operand outputBaseVertex = Local();
            node.List.AddBefore(node, new Operation(Instruction.Add, outputBaseVertex, new[] { baseVertexOffset, outputPrimVertex }));

            Operand outputPrimIndex = IncrementLocalMemory(node, resourceManager.LocalGeometryOutputIndexCountMemoryId);
            Operand baseIndexOffset = GenerateBaseOffset(resourceManager, node, definitions.GetGeometryOutputIndexBufferStride());
            Operand outputBaseIndex = Local();
            node.List.AddBefore(node, new Operation(Instruction.Add, outputBaseIndex, new[] { baseIndexOffset, outputPrimIndex }));

            node.List.AddBefore(node, new Operation(
                Instruction.Store,
                StorageKind.StorageBuffer,
                null,
                new[] { Const(ibOutputBinding), Const(0), outputBaseIndex, outputBaseVertex }));

            Operand baseOffset = Local();
            node.List.AddBefore(node, new Operation(Instruction.Multiply, baseOffset, new[] { outputBaseVertex, Const(stride) }));

            LinkedListNode<INode> newNode = node;

            for (int offset = 0; offset < stride; offset++)
            {
                Operand vertexOffset;

                if (offset > 0)
                {
                    vertexOffset = Local();
                    node.List.AddBefore(node, new Operation(Instruction.Add, vertexOffset, new[] { baseOffset, Const(offset) }));
                }
                else
                {
                    vertexOffset = baseOffset;
                }

                Operand value = Local();
                node.List.AddBefore(node, new Operation(
                    Instruction.Load,
                    StorageKind.LocalMemory,
                    value,
                    new[] { Const(resourceManager.LocalVertexDataMemoryId), Const(offset) }));

                newNode = node.List.AddBefore(node, new Operation(
                    Instruction.Store,
                    StorageKind.StorageBuffer,
                    null,
                    new[] { Const(vbOutputBinding), Const(0), vertexOffset, value }));
            }

            return newNode;
        }

        private static LinkedListNode<INode> GenerateEndPrimitive(ShaderDefinitions definitions, ResourceManager resourceManager, LinkedListNode<INode> node)
        {
            int ibOutputBinding = resourceManager.Reservations.GetGeometryIndexOutputStorageBufferBinding();

            Operand outputPrimIndex = IncrementLocalMemory(node, resourceManager.LocalGeometryOutputIndexCountMemoryId);
            Operand baseIndexOffset = GenerateBaseOffset(resourceManager, node, definitions.GetGeometryOutputIndexBufferStride());
            Operand outputBaseIndex = Local();
            node.List.AddBefore(node, new Operation(Instruction.Add, outputBaseIndex, new[] { baseIndexOffset, outputPrimIndex }));

            return node.List.AddBefore(node, new Operation(
                Instruction.Store,
                StorageKind.StorageBuffer,
                null,
                new[] { Const(ibOutputBinding), Const(0), outputBaseIndex, Const(-1) }));
        }

        private static Operand GenerateBaseOffset(ResourceManager resourceManager, LinkedListNode<INode> node, int stride)
        {
            Operand primitiveId = Local();
            GeneratePrimitiveId(resourceManager, node, primitiveId);

            Operand baseOffset = Local();
            node.List.AddBefore(node, new Operation(Instruction.Multiply, baseOffset, new[] { primitiveId, Const(stride) }));

            return baseOffset;
        }

        private static Operand IncrementLocalMemory(LinkedListNode<INode> node, int memoryId)
        {
            Operand oldValue = Local();
            node.List.AddBefore(node, new Operation(
                Instruction.Load,
                StorageKind.LocalMemory,
                oldValue,
                new[] { Const(memoryId) }));

            Operand newValue = Local();
            node.List.AddBefore(node, new Operation(Instruction.Add, newValue, new[] { oldValue, Const(1) }));

            node.List.AddBefore(node, new Operation(Instruction.Store, StorageKind.LocalMemory, null, new[] { Const(memoryId), newValue }));

            return oldValue;
        }

        private static Operand GenerateVertexOffset(
            ResourceManager resourceManager,
            LinkedListNode<INode> node,
            int verticesPerPrimitive,
            int elementOffset,
            Operand primVertex)
        {
            int vertexInfoCbBinding = resourceManager.Reservations.GetVertexInfoConstantBufferBinding();

            Operand vertexCount = Local();
            node.List.AddBefore(node, new Operation(
                Instruction.Load,
                StorageKind.ConstantBuffer,
                vertexCount,
                new[] { Const(vertexInfoCbBinding), Const(0), Const(0) }));

            Operand primInputVertex = Local();
            node.List.AddBefore(node, new Operation(
                Instruction.Load,
                StorageKind.LocalMemory,
                primInputVertex,
                new[] { Const(resourceManager.LocalTopologyRemapMemoryId), primVertex }));

            Operand instanceIndex = Local();
            node.List.AddBefore(node, new Operation(
                Instruction.Load,
                StorageKind.Input,
                instanceIndex,
                new[] { Const((int)IoVariable.GlobalInvocationId), Const(1) }));

            Operand baseVertex = Local();
            node.List.AddBefore(node, new Operation(Instruction.Multiply, baseVertex, new[] { instanceIndex, vertexCount }));

            Operand vertexIndex = Local();
            node.List.AddBefore(node, new Operation( Instruction.Add, vertexIndex, new[] { baseVertex, primInputVertex }));

            Operand vertexBaseOffset = Local();
            node.List.AddBefore(node, new Operation(
                Instruction.Multiply,
                vertexBaseOffset,
                new[] { vertexIndex, Const(resourceManager.Reservations.InputSizePerInvocation) }));

            Operand vertexElemOffset;

            if (elementOffset != 0)
            {
                vertexElemOffset = Local();

                node.List.AddBefore(node, new Operation(Instruction.Add, vertexElemOffset, new[] { vertexBaseOffset, Const(elementOffset) }));
            }
            else
            {
                vertexElemOffset = vertexBaseOffset;
            }

            return vertexElemOffset;
        }

        private static LinkedListNode<INode> GeneratePrimitiveId(ResourceManager resourceManager, LinkedListNode<INode> node, Operand dest)
        {
            int vertexInfoCbBinding = resourceManager.Reservations.GetVertexInfoConstantBufferBinding();

            Operand vertexCount = Local();
            node.List.AddBefore(node, new Operation(
                Instruction.Load,
                StorageKind.ConstantBuffer,
                vertexCount,
                new[] { Const(vertexInfoCbBinding), Const(0), Const(0) }));

            Operand vertexIndex = Local();
            node.List.AddBefore(node, new Operation(
                Instruction.Load,
                StorageKind.Input,
                vertexIndex,
                new[] { Const((int)IoVariable.GlobalInvocationId), Const(0) }));

            Operand instanceIndex = Local();
            node.List.AddBefore(node, new Operation(
                Instruction.Load,
                StorageKind.Input,
                instanceIndex,
                new[] { Const((int)IoVariable.GlobalInvocationId), Const(1) }));

            Operand baseVertex = Local();
            node.List.AddBefore(node, new Operation(Instruction.Multiply, baseVertex, new[] { instanceIndex, vertexCount }));

            return node.List.AddBefore(node, new Operation( Instruction.Add, dest, new[] { baseVertex, vertexIndex }));
        }

        private static bool TryGetOffset(ResourceManager resourceManager, Operation operation, StorageKind storageKind, out int outputOffset)
        {
            bool isStore = operation.Inst == Instruction.Store;

            IoVariable ioVariable = (IoVariable)operation.GetSource(0).Value;

            bool isValidOutput;

            if (ioVariable == IoVariable.UserDefined)
            {
                int lastIndex = operation.SourcesCount - (isStore ? 2 : 1);

                int location = operation.GetSource(1).Value;
                int component = operation.GetSource(lastIndex).Value;

                isValidOutput = resourceManager.Reservations.TryGetOffset(storageKind, location, component, out outputOffset);
            }
            else
            {
                if (ResourceReservations.IsVectorVariable(ioVariable))
                {
                    int component = operation.GetSource(operation.SourcesCount - (isStore ? 2 : 1)).Value;

                    isValidOutput = resourceManager.Reservations.TryGetOffset(storageKind, ioVariable, component, out outputOffset);
                }
                else
                {
                    isValidOutput = resourceManager.Reservations.TryGetOffset(storageKind, ioVariable, out outputOffset);
                }
            }

            return isValidOutput;
        }
    }
}
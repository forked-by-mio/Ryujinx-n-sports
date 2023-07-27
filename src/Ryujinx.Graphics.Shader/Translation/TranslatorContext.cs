using Ryujinx.Graphics.Shader.Decoders;
using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.StructuredIr;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using static Ryujinx.Graphics.Shader.IntermediateRepresentation.OperandHelper;
using static Ryujinx.Graphics.Shader.Translation.Translator;

namespace Ryujinx.Graphics.Shader.Translation
{
    public class TranslatorContext
    {
        private readonly DecodedProgram _program;
        private readonly int _localMemorySize;
        private IoUsage _vertexOutput;

        public ulong Address { get; }
        public int Size { get; }
        public int Cb1DataSize => _program.Cb1DataSize;

        internal bool HasLayerInputAttribute { get; private set; }
        internal int GpLayerInputAttribute { get; private set; }

        internal AttributeUsage AttributeUsage => _program.AttributeUsage;

        internal ShaderDefinitions Definitions { get; }

        public ShaderStage Stage => Definitions.Stage;

        internal IGpuAccessor GpuAccessor { get; }

        internal TranslationOptions Options { get; }

        internal FeatureFlags UsedFeatures { get; private set; }

        public bool LayerOutputWritten { get; private set; }
        public int LayerOutputAttribute { get; private set; }

        internal TranslatorContext(
            ulong address,
            int size,
            int localMemorySize,
            ShaderDefinitions definitions,
            IGpuAccessor gpuAccessor,
            TranslationOptions options,
            DecodedProgram program)
        {
            Address = address;
            Size = size;
            _program = program;
            _localMemorySize = localMemorySize;
            _vertexOutput = new IoUsage(FeatureFlags.None, 0, -1);
            Definitions = definitions;
            GpuAccessor = gpuAccessor;
            Options = options;
            UsedFeatures = program.UsedFeatures;
        }

        private static bool IsLoadUserDefined(Operation operation)
        {
            // TODO: Check if sources count match and all sources are constant.
            return operation.Inst == Instruction.Load && (IoVariable)operation.GetSource(0).Value == IoVariable.UserDefined;
        }

        private static bool IsStoreUserDefined(Operation operation)
        {
            // TODO: Check if sources count match and all sources are constant.
            return operation.Inst == Instruction.Store && (IoVariable)operation.GetSource(0).Value == IoVariable.UserDefined;
        }

        private static FunctionCode[] Combine(FunctionCode[] a, FunctionCode[] b, int aStart)
        {
            // Here we combine two shaders.
            // For shader A:
            // - All user attribute stores on shader A are turned into copies to a
            // temporary variable. It's assumed that shader B will consume them.
            // - All return instructions are turned into branch instructions, the
            // branch target being the start of the shader B code.
            // For shader B:
            // - All user attribute loads on shader B are turned into copies from a
            // temporary variable, as long that attribute is written by shader A.
            FunctionCode[] output = new FunctionCode[a.Length + b.Length - 1];

            List<Operation> ops = new List<Operation>(a.Length + b.Length);

            Operand[] temps = new Operand[AttributeConsts.UserAttributesCount * 4];

            Operand lblB = Label();

            for (int index = aStart; index < a[0].Code.Length; index++)
            {
                Operation operation = a[0].Code[index];

                if (IsStoreUserDefined(operation))
                {
                    int tIndex = operation.GetSource(1).Value * 4 + operation.GetSource(2).Value;

                    Operand temp = temps[tIndex];

                    if (temp == null)
                    {
                        temp = Local();

                        temps[tIndex] = temp;
                    }

                    operation.Dest = temp;
                    operation.TurnIntoCopy(operation.GetSource(operation.SourcesCount - 1));
                }

                if (operation.Inst == Instruction.Return)
                {
                    ops.Add(new Operation(Instruction.Branch, lblB));
                }
                else
                {
                    ops.Add(operation);
                }
            }

            ops.Add(new Operation(Instruction.MarkLabel, lblB));

            for (int index = 0; index < b[0].Code.Length; index++)
            {
                Operation operation = b[0].Code[index];

                if (IsLoadUserDefined(operation))
                {
                    int tIndex = operation.GetSource(1).Value * 4 + operation.GetSource(2).Value;

                    Operand temp = temps[tIndex];

                    if (temp != null)
                    {
                        operation.TurnIntoCopy(temp);
                    }
                }

                ops.Add(operation);
            }

            output[0] = new FunctionCode(ops.ToArray());

            for (int i = 1; i < a.Length; i++)
            {
                output[i] = a[i];
            }

            for (int i = 1; i < b.Length; i++)
            {
                output[a.Length + i - 1] = b[i];
            }

            return output;
        }

        internal int GetDepthRegister()
        {
            // The depth register is always two registers after the last color output.
            return BitOperations.PopCount((uint)Definitions.OmapTargets) + 1;
        }

        private void InheritFrom(TranslatorContext other)
        {
            UsedFeatures |= other.UsedFeatures;

            AttributeUsage.InheritFrom(other.AttributeUsage);
        }

        public void SetLayerOutputAttribute(int attr)
        {
            LayerOutputWritten = true;
            LayerOutputAttribute = attr;
        }

        public void SetGeometryShaderLayerInputAttribute(int attr)
        {
            UsedFeatures |= FeatureFlags.RtLayer;
            HasLayerInputAttribute = true;
            GpLayerInputAttribute = attr;
        }

        public void SetLastInVertexPipeline()
        {
            Definitions.LastInVertexPipeline = true;
        }

        public void SetNextStage(TranslatorContext nextStage)
        {
            AttributeUsage.MergeFromtNextStage(Definitions.GpPassthrough, nextStage.UsedFeatures.HasFlag(FeatureFlags.FixedFuncAttr), nextStage.AttributeUsage);

            // We don't consider geometry shaders using the geometry shader passthrough feature
            // as being the last because when this feature is used, it can't actually modify any of the outputs,
            // so the stage that comes before it is the last one that can do modifications.
            if (nextStage.Definitions.Stage != ShaderStage.Fragment && (nextStage.Definitions.Stage != ShaderStage.Geometry || !nextStage.Definitions.GpPassthrough))
            {
                Definitions.LastInVertexPipeline = false;
            }
        }

        public void MergeOutputUserAttributes(int mask, IEnumerable<int> perPatch)
        {
            AttributeUsage.MergeOutputUserAttributes(Definitions.GpPassthrough, mask, perPatch);
        }

        public ShaderProgram Translate(bool asCompute = false)
        {
            if (asCompute)
            {
                // TODO: Stop doing this here and pass used features to the emitter context.
                UsedFeatures |= FeatureFlags.VtgAsCompute;
            }

            ResourceManager resourceManager = CreateResourceManager(asCompute);

            bool usesLocalMemory = _program.UsedFeatures.HasFlag(FeatureFlags.LocalMemory);

            resourceManager.SetCurrentLocalMemory(_localMemorySize, usesLocalMemory);

            if (Stage == ShaderStage.Compute)
            {
                bool usesSharedMemory = _program.UsedFeatures.HasFlag(FeatureFlags.SharedMemory);

                resourceManager.SetCurrentSharedMemory(GpuAccessor.QueryComputeSharedMemorySize(), usesSharedMemory);
            }

            FunctionCode[] code = EmitShader(this, resourceManager, _program, initializeOutputs: true, out _);

            return Translator.Translate(
                code,
                AttributeUsage,
                GetDefinitions(asCompute),
                resourceManager,
                GpuAccessor,
                Options,
                GetUsedFeatures(asCompute),
                _program.ClipDistancesWritten);
        }

        public ShaderProgram Translate(TranslatorContext other, bool asCompute = false)
        {
            if (asCompute)
            {
                // TODO: Stop doing this here and pass used features to the emitter context.
                UsedFeatures |= FeatureFlags.VtgAsCompute;
            }

            ResourceManager resourceManager = CreateResourceManager(asCompute);

            bool usesLocalMemory = _program.UsedFeatures.HasFlag(FeatureFlags.LocalMemory);
            resourceManager.SetCurrentLocalMemory(_localMemorySize, usesLocalMemory);

            FunctionCode[] code = EmitShader(this, resourceManager, _program, initializeOutputs: false, out _);

            other.MergeOutputUserAttributes(AttributeUsage.UsedOutputAttributes, Enumerable.Empty<int>());

            bool otherUsesLocalMemory = other._program.UsedFeatures.HasFlag(FeatureFlags.LocalMemory);
            resourceManager.SetCurrentLocalMemory(other._localMemorySize, otherUsesLocalMemory);

            FunctionCode[] otherCode = EmitShader(other, resourceManager, other._program, initializeOutputs: true, out int aStart);

            code = Combine(otherCode, code, aStart);

            InheritFrom(other);

            return Translator.Translate(
                code,
                AttributeUsage,
                GetDefinitions(asCompute),
                resourceManager,
                GpuAccessor,
                Options,
                GetUsedFeatures(asCompute),
                (byte)(_program.ClipDistancesWritten | other._program.ClipDistancesWritten));
        }

        private ResourceManager CreateResourceManager(bool vertexAsCompute)
        {
            bool isTransformFeedbackEmulated = !GpuAccessor.QueryHostSupportsTransformFeedback() && GpuAccessor.QueryTransformFeedbackEnabled();

            ResourceManager resourceManager = new ResourceManager(
                Definitions.Stage,
                GpuAccessor,
                isTransformFeedbackEmulated,
                vertexAsCompute,
                _vertexOutput,
                _program.GetIoUsage());

            if (isTransformFeedbackEmulated)
            {
                StructureType tfeInfoStruct = new StructureType(new StructureField[]
                {
                    new StructureField(AggregateType.Array | AggregateType.U32, "base_offset", 4),
                    new StructureField(AggregateType.U32, "vertex_count")
                });

                int tfeInfoSbBinding = resourceManager.Reservations.GetTfeInfoStorageBufferBinding();

                BufferDefinition tfeInfoBuffer = new BufferDefinition(BufferLayout.Std430, 1, tfeInfoSbBinding, "tfe_info", tfeInfoStruct);

                resourceManager.Properties.AddOrUpdateStorageBuffer(tfeInfoSbBinding, tfeInfoBuffer);

                StructureType tfeDataStruct = new StructureType(new StructureField[]
                {
                    new StructureField(AggregateType.Array | AggregateType.U32, "data", 0)
                });

                for (int i = 0; i < ResourceReservations.TfeBuffersCount; i++)
                {
                    int binding = resourceManager.Reservations.GetTfeBufferStorageBufferBinding(i);
                    BufferDefinition tfeDataBuffer = new BufferDefinition(BufferLayout.Std430, 1, binding, $"tfe_data{i}", tfeDataStruct);
                    resourceManager.Properties.AddOrUpdateStorageBuffer(binding, tfeDataBuffer);
                }
            }

            if (vertexAsCompute)
            {
                StructureType vertexInfoStruct = new StructureType(new StructureField[]
                {
                    new StructureField(AggregateType.Vector4 | AggregateType.U32, "vertex_counts"),
                    new StructureField(AggregateType.Vector4 | AggregateType.U32, "geometry_counts"),
                    new StructureField(AggregateType.Array | AggregateType.Vector4 | AggregateType.U32, "vertex_strides", ResourceReservations.MaxVertexBufferTextures),
                    new StructureField(AggregateType.Array | AggregateType.U32, "vertex_divisors", ResourceReservations.MaxVertexBufferTextures),
                });

                int vertexInfoCbBinding = resourceManager.Reservations.GetVertexInfoConstantBufferBinding();
                BufferDefinition vertexInfoBuffer = new BufferDefinition(BufferLayout.Std140, 0, vertexInfoCbBinding, "vb_info", vertexInfoStruct);
                resourceManager.Properties.AddOrUpdateConstantBuffer(vertexInfoCbBinding, vertexInfoBuffer);

                StructureType vertexOutputStruct = new StructureType(new StructureField[]
                {
                    new StructureField(AggregateType.Array | AggregateType.FP32, "data", 0)
                });

                int vertexOutputSbBinding = resourceManager.Reservations.GetVertexOutputStorageBufferBinding();
                BufferDefinition vertexOutputBuffer = new BufferDefinition(BufferLayout.Std430, 1, vertexOutputSbBinding, "vertex_output", vertexOutputStruct);
                resourceManager.Properties.AddOrUpdateStorageBuffer(vertexOutputSbBinding, vertexOutputBuffer);

                if (Stage == ShaderStage.Vertex)
                {
                    int ibBinding = resourceManager.Reservations.GetIndexBufferTextureBinding();
                    TextureDefinition indexBuffer = new TextureDefinition(2, ibBinding, "ib_data", SamplerType.TextureBuffer, TextureFormat.Unknown, TextureUsageFlags.None);
                    resourceManager.Properties.AddOrUpdateTexture(ibBinding, indexBuffer);

                    int inputMap = _program.AttributeUsage.UsedInputAttributes;

                    while (inputMap != 0)
                    {
                        int location = BitOperations.TrailingZeroCount(inputMap);
                        int binding = resourceManager.Reservations.GetVertexBufferTextureBinding(location);
                        TextureDefinition vaBuffer = new TextureDefinition(2, binding, $"vb_data{location}", SamplerType.TextureBuffer, TextureFormat.Unknown, TextureUsageFlags.None);
                        resourceManager.Properties.AddOrUpdateTexture(binding, vaBuffer);

                        inputMap &= ~(1 << location);
                    }
                }
                else if (Stage == ShaderStage.Geometry)
                {
                    int trbBinding = resourceManager.Reservations.GetTopologyRemapBufferTextureBinding();
                    TextureDefinition remapBuffer = new TextureDefinition(2, trbBinding, "trb_data", SamplerType.TextureBuffer, TextureFormat.Unknown, TextureUsageFlags.None);
                    resourceManager.Properties.AddOrUpdateTexture(trbBinding, remapBuffer);

                    int geometryVbOutputSbBinding = resourceManager.Reservations.GetGeometryVertexOutputStorageBufferBinding();
                    BufferDefinition geometryVbOutputBuffer = new BufferDefinition(BufferLayout.Std430, 1, geometryVbOutputSbBinding, "geometry_vb_output", vertexOutputStruct);
                    resourceManager.Properties.AddOrUpdateStorageBuffer(geometryVbOutputSbBinding, geometryVbOutputBuffer);

                    StructureType geometryIbOutputStruct = new StructureType(new StructureField[]
                    {
                        new StructureField(AggregateType.Array | AggregateType.U32, "data", 0)
                    });

                    int geometryIbOutputSbBinding = resourceManager.Reservations.GetGeometryIndexOutputStorageBufferBinding();
                    BufferDefinition geometryIbOutputBuffer = new BufferDefinition(BufferLayout.Std430, 1, geometryIbOutputSbBinding, "geometry_ib_output", geometryIbOutputStruct);
                    resourceManager.Properties.AddOrUpdateStorageBuffer(geometryIbOutputSbBinding, geometryIbOutputBuffer);
                }

                resourceManager.SetVertexAsComputeLocalMemories(Definitions.Stage, Definitions.InputTopology);
            }

            return resourceManager;
        }

        private ShaderDefinitions GetDefinitions(bool vertexAsCompute)
        {
            if (vertexAsCompute)
            {
                return Definitions.AsCompute(32, 32, 1);
            }
            else
            {
                return Definitions;
            }
        }

        private FeatureFlags GetUsedFeatures(bool vertexAsCompute)
        {
            if (vertexAsCompute)
            {
                return UsedFeatures | FeatureFlags.VtgAsCompute;
            }
            else
            {
                return UsedFeatures;
            }
        }

        public ResourceReservations GetResourceReservations()
        {
            bool isTransformFeedbackEmulated = !GpuAccessor.QueryHostSupportsTransformFeedback() && GpuAccessor.QueryTransformFeedbackEnabled();

            return new ResourceReservations(
                GpuAccessor,
                isTransformFeedbackEmulated,
                vertexAsCompute: true,
                _vertexOutput,
                _program.GetIoUsage());
        }

        public void SetVertexOutputMapForGeometryAsCompute(TranslatorContext vertexContext)
        {
            _vertexOutput = vertexContext._program.GetIoUsage();
        }

        public ShaderProgram GenerateVertexPassthroughForCompute()
        {
            const int VertexInfoCbBinding = 1;
            const int VertexDataSbBinding = 0;

            var attributeUsage = new AttributeUsage(GpuAccessor);
            var resourceManager = new ResourceManager(ShaderStage.Vertex, GpuAccessor);

            if (Stage == ShaderStage.Vertex)
            {
                StructureType vertexInfoStruct = new StructureType(new StructureField[]
                {
                    new StructureField(AggregateType.Vector4 | AggregateType.U32, "vertex_counts"),
                });

                BufferDefinition vertexInfoBuffer = new BufferDefinition(BufferLayout.Std140, 0, VertexInfoCbBinding, "vb_info", vertexInfoStruct);
                resourceManager.Properties.AddOrUpdateConstantBuffer(VertexInfoCbBinding, vertexInfoBuffer);
            }

            StructureType vertexInputStruct = new StructureType(new StructureField[]
            {
                new StructureField(AggregateType.Array | AggregateType.FP32, "data", 0)
            });

            BufferDefinition vertexOutputBuffer = new BufferDefinition(BufferLayout.Std430, 1, VertexDataSbBinding, "vb_input", vertexInputStruct);
            resourceManager.Properties.AddOrUpdateStorageBuffer(VertexDataSbBinding, vertexOutputBuffer);

            var reservationsForVertexAsCompute = GetResourceReservations();

            var context = new EmitterContext();

            Operand vertexIndex = Options.TargetApi == TargetApi.OpenGL
                ? context.Load(StorageKind.Input, IoVariable.VertexId)
                : context.Load(StorageKind.Input, IoVariable.VertexIndex);

            if (Stage == ShaderStage.Vertex)
            {
                Operand vertexCount = context.Load(StorageKind.ConstantBuffer, VertexInfoCbBinding, Const(0), Const(0));

                // Base instance will be always zero when this shader is used, so which one we use here doesn't really matter.
                Operand instanceId = Options.TargetApi == TargetApi.OpenGL
                    ? context.Load(StorageKind.Input, IoVariable.InstanceId)
                    : context.Load(StorageKind.Input, IoVariable.InstanceIndex);

                vertexIndex = context.IAdd(context.IMultiply(instanceId, vertexCount), vertexIndex);
            }

            Operand baseOffset = context.IMultiply(vertexIndex, Const(reservationsForVertexAsCompute.OutputSizePerInvocation));

            foreach ((IoDefinition ioDefinition, int inputOffset) in reservationsForVertexAsCompute.Offsets)
            {
                if (ioDefinition.StorageKind != StorageKind.Output)
                {
                    continue;
                }

                Operand vertexOffset = inputOffset != 0 ? context.IAdd(baseOffset, Const(inputOffset)) : baseOffset;
                Operand value = context.Load(StorageKind.StorageBuffer, VertexDataSbBinding, Const(0), vertexOffset);

                if (ioDefinition.IoVariable == IoVariable.UserDefined)
                {
                    context.Store(StorageKind.Output, ioDefinition.IoVariable, null, Const(ioDefinition.Location), Const(ioDefinition.Component), value);
                    attributeUsage.SetOutputUserAttribute(ioDefinition.Location);
                }
                else if (ResourceReservations.IsVectorOrArrayVariable(ioDefinition.IoVariable))
                {
                    context.Store(StorageKind.Output, ioDefinition.IoVariable, null, Const(ioDefinition.Component), value);
                }
                else
                {
                    context.Store(StorageKind.Output, ioDefinition.IoVariable, null, value);
                }
            }

            var operations = context.GetOperations();
            var cfg = ControlFlowGraph.Create(operations);
            var function = new Function(cfg.Blocks, "main", false, 0, 0);

            var definitions = new ShaderDefinitions(ShaderStage.Vertex);

            return Translator.Generate(
                new[] { function },
                attributeUsage,
                definitions,
                resourceManager,
                GpuAccessor,
                FeatureFlags.None,
                0,
                Options);
        }

        public ShaderProgram GenerateGeometryPassthrough()
        {
            int outputAttributesMask = AttributeUsage.UsedOutputAttributes;
            int layerOutputAttr = LayerOutputAttribute;

            OutputTopology outputTopology;
            int maxOutputVertices;

            switch (GpuAccessor.QueryPrimitiveTopology())
            {
                case InputTopology.Points:
                    outputTopology = OutputTopology.PointList;
                    maxOutputVertices = 1;
                    break;
                case InputTopology.Lines:
                case InputTopology.LinesAdjacency:
                    outputTopology = OutputTopology.LineStrip;
                    maxOutputVertices = 2;
                    break;
                default:
                    outputTopology = OutputTopology.TriangleStrip;
                    maxOutputVertices = 3;
                    break;
            }

            var attributeUsage = new AttributeUsage(GpuAccessor);
            var resourceManager = new ResourceManager(ShaderStage.Geometry, GpuAccessor);

            var context = new EmitterContext();

            for (int v = 0; v < maxOutputVertices; v++)
            {
                int outAttrsMask = outputAttributesMask;

                while (outAttrsMask != 0)
                {
                    int attrIndex = BitOperations.TrailingZeroCount(outAttrsMask);

                    outAttrsMask &= ~(1 << attrIndex);

                    for (int c = 0; c < 4; c++)
                    {
                        int attr = AttributeConsts.UserAttributeBase + attrIndex * 16 + c * 4;

                        Operand value = context.Load(StorageKind.Input, IoVariable.UserDefined, Const(attrIndex), Const(v), Const(c));

                        if (attr == layerOutputAttr)
                        {
                            context.Store(StorageKind.Output, IoVariable.Layer, null, value);
                        }
                        else
                        {
                            context.Store(StorageKind.Output, IoVariable.UserDefined, null, Const(attrIndex), Const(c), value);
                            attributeUsage.SetOutputUserAttribute(attrIndex);
                        }

                        attributeUsage.SetInputUserAttribute(attrIndex, c);
                    }
                }

                for (int c = 0; c < 4; c++)
                {
                    Operand value = context.Load(StorageKind.Input, IoVariable.Position, Const(v), Const(c));

                    context.Store(StorageKind.Output, IoVariable.Position, null, Const(c), value);
                }

                context.EmitVertex();
            }

            context.EndPrimitive();

            var operations = context.GetOperations();
            var cfg = ControlFlowGraph.Create(operations);
            var function = new Function(cfg.Blocks, "main", false, 0, 0);

            var definitions = new ShaderDefinitions(
                ShaderStage.Geometry,
                false,
                1,
                GpuAccessor.QueryPrimitiveTopology(),
                outputTopology,
                maxOutputVertices);

            return Translator.Generate(
                new[] { function },
                attributeUsage,
                definitions,
                resourceManager,
                GpuAccessor,
                FeatureFlags.RtLayer,
                0,
                Options);
        }
    }
}

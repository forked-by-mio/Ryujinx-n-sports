using Ryujinx.Common;
using Ryujinx.Graphics.Shader.StructuredIr;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace Ryujinx.Graphics.Shader.CodeGen.Glsl
{
    static class Declarations
    {
        public static void Declare(CodeGenContext context, StructuredProgramInfo info)
        {
            context.AppendLine(context.TargetApi == TargetApi.Vulkan ? "#version 460 core" : "#version 450 core");
            context.AppendLine("#extension GL_ARB_gpu_shader_int64 : enable");

            if (context.HostCapabilities.SupportsShaderBallot)
            {
                context.AppendLine("#extension GL_ARB_shader_ballot : enable");
            }
            else
            {
                context.AppendLine("#extension GL_KHR_shader_subgroup_basic : enable");
                context.AppendLine("#extension GL_KHR_shader_subgroup_ballot : enable");
            }

            context.AppendLine("#extension GL_ARB_shader_group_vote : enable");
            context.AppendLine("#extension GL_EXT_shader_image_load_formatted : enable");
            context.AppendLine("#extension GL_EXT_texture_shadow_lod : enable");

            if (context.Definitions.Stage == ShaderStage.Compute)
            {
                context.AppendLine("#extension GL_ARB_compute_shader : enable");
            }
            else if (context.Definitions.Stage == ShaderStage.Fragment)
            {
                if (context.HostCapabilities.SupportsFragmentShaderInterlock)
                {
                    context.AppendLine("#extension GL_ARB_fragment_shader_interlock : enable");
                }
                else if (context.HostCapabilities.SupportsFragmentShaderOrderingIntel)
                {
                    context.AppendLine("#extension GL_INTEL_fragment_shader_ordering : enable");
                }
            }
            else
            {
                if (context.Definitions.Stage == ShaderStage.Vertex)
                {
                    context.AppendLine("#extension GL_ARB_shader_draw_parameters : enable");
                }

                context.AppendLine("#extension GL_ARB_shader_viewport_layer_array : enable");
            }

            if (context.Definitions.GpPassthrough && context.HostCapabilities.SupportsGeometryShaderPassthrough)
            {
                context.AppendLine("#extension GL_NV_geometry_shader_passthrough : enable");
            }

            if (context.HostCapabilities.SupportsViewportMask)
            {
                context.AppendLine("#extension GL_NV_viewport_array2 : enable");
            }

            context.AppendLine("#pragma optionNV(fastmath off)");
            context.AppendLine();

            context.AppendLine($"const int {DefaultNames.UndefinedName} = 0;");
            context.AppendLine();

            DeclareConstantBuffers(context, context.Properties.ConstantBuffers.Values);
            DeclareStorageBuffers(context, context.Properties.StorageBuffers.Values);
            DeclareMemories(context, context.Properties.LocalMemories.Values, isShared: false);
            DeclareMemories(context, context.Properties.SharedMemories.Values, isShared: true);
            DeclareSamplers(context, context.Properties.Textures.Values);
            DeclareImages(context, context.Properties.Images.Values);

            if (context.Definitions.Stage != ShaderStage.Compute)
            {
                if (context.Definitions.Stage == ShaderStage.Geometry)
                {
                    string inPrimitive = context.Definitions.InputTopology.ToGlslString();

                    context.AppendLine($"layout (invocations = {context.Definitions.ThreadsPerInputPrimitive}, {inPrimitive}) in;");

                    if (context.Definitions.GpPassthrough && context.HostCapabilities.SupportsGeometryShaderPassthrough)
                    {
                        context.AppendLine($"layout (passthrough) in gl_PerVertex");
                        context.EnterScope();
                        context.AppendLine("vec4 gl_Position;");
                        context.AppendLine("float gl_PointSize;");
                        context.AppendLine("float gl_ClipDistance[];");
                        context.LeaveScope(";");
                    }
                    else
                    {
                        string outPrimitive = context.Definitions.OutputTopology.ToGlslString();

                        int maxOutputVertices = context.Definitions.GpPassthrough
                            ? context.Definitions.InputTopology.ToInputVerticesNoAdjacency()
                            : context.Definitions.MaxOutputVertices;

                        context.AppendLine($"layout ({outPrimitive}, max_vertices = {maxOutputVertices}) out;");
                    }

                    context.AppendLine();
                }
                else if (context.Definitions.Stage == ShaderStage.TessellationControl)
                {
                    int threadsPerInputPrimitive = context.Definitions.ThreadsPerInputPrimitive;

                    context.AppendLine($"layout (vertices = {threadsPerInputPrimitive}) out;");
                    context.AppendLine();
                }
                else if (context.Definitions.Stage == ShaderStage.TessellationEvaluation)
                {
                    bool tessCw = context.Definitions.TessCw;

                    if (context.TargetApi == TargetApi.Vulkan)
                    {
                        // We invert the front face on Vulkan backend, so we need to do that here aswell.
                        tessCw = !tessCw;
                    }

                    string patchType = context.Definitions.TessPatchType.ToGlsl();
                    string spacing = context.Definitions.TessSpacing.ToGlsl();
                    string windingOrder = tessCw ? "cw" : "ccw";

                    context.AppendLine($"layout ({patchType}, {spacing}, {windingOrder}) in;");
                    context.AppendLine();
                }

                if (context.AttributeUsage.UsedInputAttributes != 0 || context.Definitions.GpPassthrough)
                {
                    DeclareInputAttributes(context, info);

                    context.AppendLine();
                }

                if (context.AttributeUsage.UsedOutputAttributes != 0 || context.Definitions.Stage != ShaderStage.Fragment)
                {
                    DeclareOutputAttributes(context, info);

                    context.AppendLine();
                }

                if (context.AttributeUsage.UsedInputAttributesPerPatch.Count != 0)
                {
                    DeclareInputAttributesPerPatch(context, context.AttributeUsage.UsedInputAttributesPerPatch);

                    context.AppendLine();
                }

                if (context.AttributeUsage.UsedOutputAttributesPerPatch.Count != 0)
                {
                    DeclareUsedOutputAttributesPerPatch(context, context.AttributeUsage.UsedOutputAttributesPerPatch);

                    context.AppendLine();
                }

                if (context.Definitions.TransformFeedbackEnabled && context.Definitions.LastInVertexPipeline)
                {
                    var tfOutput = context.Definitions.GetTransformFeedbackOutput(AttributeConsts.PositionX);
                    if (tfOutput.Valid)
                    {
                        context.AppendLine($"layout (xfb_buffer = {tfOutput.Buffer}, xfb_offset = {tfOutput.Offset}, xfb_stride = {tfOutput.Stride}) out gl_PerVertex");
                        context.EnterScope();
                        context.AppendLine("vec4 gl_Position;");
                        context.LeaveScope(context.Definitions.Stage == ShaderStage.TessellationControl ? " gl_out[];" : ";");
                    }
                }
            }
            else
            {
                string localSizeX = NumberFormatter.FormatInt(context.Definitions.ComputeLocalSizeX);
                string localSizeY = NumberFormatter.FormatInt(context.Definitions.ComputeLocalSizeY);
                string localSizeZ = NumberFormatter.FormatInt(context.Definitions.ComputeLocalSizeZ);

                context.AppendLine(
                    "layout (" +
                    $"local_size_x = {localSizeX}, " +
                    $"local_size_y = {localSizeY}, " +
                    $"local_size_z = {localSizeZ}) in;");
                context.AppendLine();
            }

            if (context.Definitions.Stage == ShaderStage.Fragment && context.Definitions.EarlyZForce)
            {
                context.AppendLine("layout(early_fragment_tests) in;");
                context.AppendLine();
            }

            if ((info.HelperFunctionsMask & HelperFunctionsMask.MultiplyHighS32) != 0)
            {
                AppendHelperFunction(context, "Ryujinx.Graphics.Shader/CodeGen/Glsl/HelperFunctions/MultiplyHighS32.glsl");
            }

            if ((info.HelperFunctionsMask & HelperFunctionsMask.MultiplyHighU32) != 0)
            {
                AppendHelperFunction(context, "Ryujinx.Graphics.Shader/CodeGen/Glsl/HelperFunctions/MultiplyHighU32.glsl");
            }

            if ((info.HelperFunctionsMask & HelperFunctionsMask.Shuffle) != 0)
            {
                AppendHelperFunction(context, "Ryujinx.Graphics.Shader/CodeGen/Glsl/HelperFunctions/Shuffle.glsl");
            }

            if ((info.HelperFunctionsMask & HelperFunctionsMask.ShuffleDown) != 0)
            {
                AppendHelperFunction(context, "Ryujinx.Graphics.Shader/CodeGen/Glsl/HelperFunctions/ShuffleDown.glsl");
            }

            if ((info.HelperFunctionsMask & HelperFunctionsMask.ShuffleUp) != 0)
            {
                AppendHelperFunction(context, "Ryujinx.Graphics.Shader/CodeGen/Glsl/HelperFunctions/ShuffleUp.glsl");
            }

            if ((info.HelperFunctionsMask & HelperFunctionsMask.ShuffleXor) != 0)
            {
                AppendHelperFunction(context, "Ryujinx.Graphics.Shader/CodeGen/Glsl/HelperFunctions/ShuffleXor.glsl");
            }

            if ((info.HelperFunctionsMask & HelperFunctionsMask.SwizzleAdd) != 0)
            {
                AppendHelperFunction(context, "Ryujinx.Graphics.Shader/CodeGen/Glsl/HelperFunctions/SwizzleAdd.glsl");
            }
        }

        private static string GetTfLayout(TransformFeedbackOutput tfOutput)
        {
            if (tfOutput.Valid)
            {
                return $"layout (xfb_buffer = {tfOutput.Buffer}, xfb_offset = {tfOutput.Offset}, xfb_stride = {tfOutput.Stride}) ";
            }

            return string.Empty;
        }

        public static void DeclareLocals(CodeGenContext context, StructuredFunction function)
        {
            foreach (AstOperand decl in function.Locals)
            {
                string name = context.OperandManager.DeclareLocal(decl);

                context.AppendLine(GetVarTypeName(context, decl.VarType) + " " + name + ";");
            }
        }

        public static string GetVarTypeName(CodeGenContext context, AggregateType type, bool precise = true)
        {
            if (context.HostCapabilities.ReducedPrecision)
            {
                precise = false;
            }

            return type switch
            {
                AggregateType.Void => "void",
                AggregateType.Bool => "bool",
                AggregateType.FP32 => precise ? "precise float" : "float",
                AggregateType.FP64 => "double",
                AggregateType.S32 => "int",
                AggregateType.U32 => "uint",
                AggregateType.Vector2 | AggregateType.Bool => "bvec2",
                AggregateType.Vector2 | AggregateType.FP32 => precise ? "precise vec2" : "vec2",
                AggregateType.Vector2 | AggregateType.FP64 => "dvec2",
                AggregateType.Vector2 | AggregateType.S32 => "ivec2",
                AggregateType.Vector2 | AggregateType.U32 => "uvec2",
                AggregateType.Vector3 | AggregateType.Bool => "bvec3",
                AggregateType.Vector3 | AggregateType.FP32 => precise ? "precise vec3" : "vec3",
                AggregateType.Vector3 | AggregateType.FP64 => "dvec3",
                AggregateType.Vector3 | AggregateType.S32 => "ivec3",
                AggregateType.Vector3 | AggregateType.U32 => "uvec3",
                AggregateType.Vector4 | AggregateType.Bool => "bvec4",
                AggregateType.Vector4 | AggregateType.FP32 => precise ? "precise vec4" : "vec4",
                AggregateType.Vector4 | AggregateType.FP64 => "dvec4",
                AggregateType.Vector4 | AggregateType.S32 => "ivec4",
                AggregateType.Vector4 | AggregateType.U32 => "uvec4",
                _ => throw new ArgumentException($"Invalid variable type \"{type}\".")
            };
        }

        private static void DeclareConstantBuffers(CodeGenContext context, IEnumerable<BufferDefinition> buffers)
        {
            DeclareBuffers(context, buffers, "uniform");
        }

        private static void DeclareStorageBuffers(CodeGenContext context, IEnumerable<BufferDefinition> buffers)
        {
            DeclareBuffers(context, buffers, "buffer");
        }

        private static void DeclareBuffers(CodeGenContext context, IEnumerable<BufferDefinition> buffers, string declType)
        {
            foreach (BufferDefinition buffer in buffers)
            {
                string layout = buffer.Layout switch
                {
                    BufferLayout.Std140 => "std140",
                    _ => "std430"
                };

                string set = string.Empty;

                if (context.TargetApi == TargetApi.Vulkan)
                {
                    set = $"set = {buffer.Set}, ";
                }

                context.AppendLine($"layout ({set}binding = {buffer.Binding}, {layout}) {declType} _{buffer.Name}");
                context.EnterScope();

                foreach (StructureField field in buffer.Type.Fields)
                {
                    if (field.Type.HasFlag(AggregateType.Array))
                    {
                        string typeName = GetVarTypeName(context, field.Type & ~AggregateType.Array);

                        if (field.ArrayLength > 0)
                        {
                            string arraySize = field.ArrayLength.ToString(CultureInfo.InvariantCulture);

                            context.AppendLine($"{typeName} {field.Name}[{arraySize}];");
                        }
                        else
                        {
                            context.AppendLine($"{typeName} {field.Name}[];");
                        }
                    }
                    else
                    {
                        string typeName = GetVarTypeName(context, field.Type);

                        context.AppendLine($"{typeName} {field.Name};");
                    }
                }

                context.LeaveScope($" {buffer.Name};");
                context.AppendLine();
            }
        }

        private static void DeclareMemories(CodeGenContext context, IEnumerable<MemoryDefinition> memories, bool isShared)
        {
            string prefix = isShared ? "shared " : string.Empty;

            foreach (MemoryDefinition memory in memories)
            {
                string typeName = GetVarTypeName(context, memory.Type & ~AggregateType.Array);

                if (memory.Type.HasFlag(AggregateType.Array))
                {
                    if (memory.ArrayLength > 0)
                    {
                        string arraySize = memory.ArrayLength.ToString(CultureInfo.InvariantCulture);

                        context.AppendLine($"{prefix}{typeName} {memory.Name}[{arraySize}];");
                    }
                    else
                    {
                        context.AppendLine($"{prefix}{typeName} {memory.Name}[];");
                    }
                }
                else
                {
                    context.AppendLine($"{prefix}{typeName} {memory.Name};");
                }
            }
        }

        private static void DeclareSamplers(CodeGenContext context, IEnumerable<TextureDefinition> definitions)
        {
            int arraySize = 0;

            foreach (var definition in definitions)
            {
                string indexExpr = string.Empty;

                if (definition.Type.HasFlag(SamplerType.Indexed))
                {
                    if (arraySize == 0)
                    {
                        arraySize = ResourceManager.SamplerArraySize;
                    }
                    else if (--arraySize != 0)
                    {
                        continue;
                    }

                    indexExpr = $"[{NumberFormatter.FormatInt(arraySize)}]";
                }

                string samplerTypeName = definition.Type.ToGlslSamplerType();

                string layout = string.Empty;

                if (context.TargetApi == TargetApi.Vulkan)
                {
                    layout = $", set = {definition.Set}";
                }

                context.AppendLine($"layout (binding = {definition.Binding}{layout}) uniform {samplerTypeName} {definition.Name}{indexExpr};");
            }
        }

        private static void DeclareImages(CodeGenContext context, IEnumerable<TextureDefinition> definitions)
        {
            int arraySize = 0;

            foreach (var definition in definitions)
            {
                string indexExpr = string.Empty;

                if (definition.Type.HasFlag(SamplerType.Indexed))
                {
                    if (arraySize == 0)
                    {
                        arraySize = ResourceManager.SamplerArraySize;
                    }
                    else if (--arraySize != 0)
                    {
                        continue;
                    }

                    indexExpr = $"[{NumberFormatter.FormatInt(arraySize)}]";
                }

                string imageTypeName = definition.Type.ToGlslImageType(definition.Format.GetComponentType());

                if (definition.Flags.HasFlag(TextureUsageFlags.ImageCoherent))
                {
                    imageTypeName = "coherent " + imageTypeName;
                }

                string layout = definition.Format.ToGlslFormat();

                if (!string.IsNullOrEmpty(layout))
                {
                    layout = ", " + layout;
                }

                if (context.TargetApi == TargetApi.Vulkan)
                {
                    layout = $", set = {definition.Set}{layout}";
                }

                context.AppendLine($"layout (binding = {definition.Binding}{layout}) uniform {imageTypeName} {definition.Name}{indexExpr};");
            }
        }

        private static void DeclareInputAttributes(CodeGenContext context, StructuredProgramInfo info)
        {
            if (context.Definitions.IaIndexing)
            {
                string suffix = context.Definitions.Stage == ShaderStage.Geometry ? "[]" : string.Empty;

                context.AppendLine($"layout (location = 0) in vec4 {DefaultNames.IAttributePrefix}{suffix}[{Constants.MaxAttributes}];");
            }
            else
            {
                int usedAttributes = context.AttributeUsage.UsedInputAttributes | context.AttributeUsage.PassthroughAttributes;
                while (usedAttributes != 0)
                {
                    int index = BitOperations.TrailingZeroCount(usedAttributes);
                    DeclareInputAttribute(context, info, index);
                    usedAttributes &= ~(1 << index);
                }
            }
        }

        private static void DeclareInputAttributesPerPatch(CodeGenContext context, HashSet<int> attrs)
        {
            foreach (int attr in attrs.Order())
            {
                DeclareInputAttributePerPatch(context, attr);
            }
        }

        private static void DeclareInputAttribute(CodeGenContext context, StructuredProgramInfo info, int attr)
        {
            string suffix = IsArrayAttributeGlsl(context.Definitions.Stage, isOutAttr: false) ? "[]" : string.Empty;
            string iq = string.Empty;

            if (context.Definitions.Stage == ShaderStage.Fragment)
            {
                iq = context.Definitions.ImapTypes[attr].GetFirstUsedType() switch
                {
                    PixelImap.Constant => "flat ",
                    PixelImap.ScreenLinear => "noperspective ",
                    _ => string.Empty
                };
            }

            string name = $"{DefaultNames.IAttributePrefix}{attr}";

            if (context.Definitions.TransformFeedbackEnabled && context.Definitions.Stage == ShaderStage.Fragment)
            {
                int components = context.Definitions.GetTransformFeedbackOutputComponents(attr, 0);

                if (components > 1)
                {
                    string type = components switch
                    {
                        2 => "vec2",
                        3 => "vec3",
                        4 => "vec4",
                        _ => "float"
                    };

                    context.AppendLine($"layout (location = {attr}) in {type} {name};");
                }

                for (int c = components > 1 ? components : 0; c < 4; c++)
                {
                    char swzMask = "xyzw"[c];

                    context.AppendLine($"layout (location = {attr}, component = {c}) {iq}in float {name}_{swzMask}{suffix};");
                }
            }
            else
            {
                bool passthrough = (context.AttributeUsage.PassthroughAttributes & (1 << attr)) != 0;
                string pass = passthrough && context.HostCapabilities.SupportsGeometryShaderPassthrough ? "passthrough, " : string.Empty;
                string type = GetVarTypeName(context, context.Definitions.GetUserDefinedType(attr, isOutput: false), false);

                context.AppendLine($"layout ({pass}location = {attr}) {iq}in {type} {name}{suffix};");
            }
        }

        private static void DeclareInputAttributePerPatch(CodeGenContext context, int attr)
        {
            int location = context.AttributeUsage.GetPerPatchAttributeLocation(attr);
            string name = $"{DefaultNames.PerPatchAttributePrefix}{attr}";

            context.AppendLine($"layout (location = {location}) patch in vec4 {name};");
        }

        private static void DeclareOutputAttributes(CodeGenContext context, StructuredProgramInfo info)
        {
            if (context.Definitions.OaIndexing)
            {
                context.AppendLine($"layout (location = 0) out vec4 {DefaultNames.OAttributePrefix}[{Constants.MaxAttributes}];");
            }
            else
            {
                int usedAttributes = context.AttributeUsage.UsedOutputAttributes;

                if (context.Definitions.Stage == ShaderStage.Fragment && context.Definitions.DualSourceBlend)
                {
                    int firstOutput = BitOperations.TrailingZeroCount(usedAttributes);
                    int mask = 3 << firstOutput;

                    if ((usedAttributes & mask) == mask)
                    {
                        usedAttributes &= ~mask;
                        DeclareOutputDualSourceBlendAttribute(context, firstOutput);
                    }
                }

                while (usedAttributes != 0)
                {
                    int index = BitOperations.TrailingZeroCount(usedAttributes);
                    DeclareOutputAttribute(context, index);
                    usedAttributes &= ~(1 << index);
                }
            }
        }

        private static void DeclareOutputAttribute(CodeGenContext context, int attr)
        {
            string suffix = IsArrayAttributeGlsl(context.Definitions.Stage, isOutAttr: true) ? "[]" : string.Empty;
            string name = $"{DefaultNames.OAttributePrefix}{attr}{suffix}";

            if (context.Definitions.TransformFeedbackEnabled && context.Definitions.LastInVertexPipeline)
            {
                int components = context.Definitions.GetTransformFeedbackOutputComponents(attr, 0);

                if (components > 1)
                {
                    string type = components switch
                    {
                        2 => "vec2",
                        3 => "vec3",
                        4 => "vec4",
                        _ => "float"
                    };

                    string xfb = string.Empty;

                    var tfOutput = context.Definitions.GetTransformFeedbackOutput(attr, 0);
                    if (tfOutput.Valid)
                    {
                        xfb = $", xfb_buffer = {tfOutput.Buffer}, xfb_offset = {tfOutput.Offset}, xfb_stride = {tfOutput.Stride}";
                    }

                    context.AppendLine($"layout (location = {attr}{xfb}) out {type} {name};");
                }

                for (int c = components > 1 ? components : 0; c < 4; c++)
                {
                    char swzMask = "xyzw"[c];

                    string xfb = string.Empty;

                    var tfOutput = context.Definitions.GetTransformFeedbackOutput(attr, c);
                    if (tfOutput.Valid)
                    {
                        xfb = $", xfb_buffer = {tfOutput.Buffer}, xfb_offset = {tfOutput.Offset}, xfb_stride = {tfOutput.Stride}";
                    }

                    context.AppendLine($"layout (location = {attr}, component = {c}{xfb}) out float {name}_{swzMask};");
                }
            }
            else
            {
                string type = context.Definitions.Stage != ShaderStage.Fragment ? "vec4" :
                    GetVarTypeName(context, context.Definitions.GetFragmentOutputColorType(attr), false);

                if (context.HostCapabilities.ReducedPrecision && context.Definitions.Stage == ShaderStage.Vertex && attr == 0)
                {
                    context.AppendLine($"layout (location = {attr}) invariant out {type} {name};");
                }
                else
                {
                    context.AppendLine($"layout (location = {attr}) out {type} {name};");
                }
            }
        }

        private static void DeclareOutputDualSourceBlendAttribute(CodeGenContext context, int attr)
        {
            string name = $"{DefaultNames.OAttributePrefix}{attr}";
            string name2 = $"{DefaultNames.OAttributePrefix}{(attr + 1)}";

            context.AppendLine($"layout (location = {attr}, index = 0) out vec4 {name};");
            context.AppendLine($"layout (location = {attr}, index = 1) out vec4 {name2};");
        }

        private static bool IsArrayAttributeGlsl(ShaderStage stage, bool isOutAttr)
        {
            if (isOutAttr)
            {
                return stage == ShaderStage.TessellationControl;
            }
            else
            {
                return stage == ShaderStage.TessellationControl ||
                       stage == ShaderStage.TessellationEvaluation ||
                       stage == ShaderStage.Geometry;
            }
        }

        private static void DeclareUsedOutputAttributesPerPatch(CodeGenContext context, HashSet<int> attrs)
        {
            foreach (int attr in attrs.Order())
            {
                DeclareOutputAttributePerPatch(context, attr);
            }
        }

        private static void DeclareOutputAttributePerPatch(CodeGenContext context, int attr)
        {
            int location = context.AttributeUsage.GetPerPatchAttributeLocation(attr);
            string name = $"{DefaultNames.PerPatchAttributePrefix}{attr}";

            context.AppendLine($"layout (location = {location}) patch out vec4 {name};");
        }

        private static void AppendHelperFunction(CodeGenContext context, string filename)
        {
            string code = EmbeddedResources.ReadAllText(filename);

            code = code.Replace("\t", CodeGenContext.Tab);

            if (context.HostCapabilities.SupportsShaderBallot)
            {
                code = code.Replace("$SUBGROUP_INVOCATION$", "gl_SubGroupInvocationARB");
                code = code.Replace("$SUBGROUP_BROADCAST$", "readInvocationARB");
            }
            else
            {
                code = code.Replace("$SUBGROUP_INVOCATION$", "gl_SubgroupInvocationID");
                code = code.Replace("$SUBGROUP_BROADCAST$", "subgroupBroadcast");
            }

            context.AppendLine(code);
            context.AppendLine();
        }
    }
}
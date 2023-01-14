﻿using Ryujinx.Graphics.Device;
using Ryujinx.Graphics.Gpu.Memory;
using Ryujinx.Graphics.Vic.Image;
using Ryujinx.Graphics.Vic.Types;
using System;
using System.Collections.Generic;

namespace Ryujinx.Graphics.Vic
{
    public class VicDevice : IDeviceState
    {
        private readonly MemoryManager _gmm;
        private readonly ResourceManager _rm;
        private readonly DeviceState<VicRegisters> _state;

        public VicDevice(MemoryManager gmm)
        {
            _gmm = gmm;
            _rm = new ResourceManager(gmm, new BufferPool<Pixel>(), new BufferPool<byte>());
            _state = new DeviceState<VicRegisters>(new Dictionary<string, RwCallback>
            {
                { nameof(VicRegisters.Execute), new RwCallback(Execute, null) }
            });
        }

        public int Read(int offset) => _state.Read(offset);
        public void Write(int offset, int data) => _state.Write(offset, data);

        private void Execute(int data)
        {
            ConfigStruct config = ReadIndirect<ConfigStruct>(_state.State.SetConfigStructOffset);

            using Surface output = new Surface(
                _rm.SurfacePool,
                config.OutputSurfaceConfig.OutSurfaceWidth + 1,
                config.OutputSurfaceConfig.OutSurfaceHeight + 1);

            for (int i = 0; i < config.SlotStruct.Length; i++)
            {
                ref SlotStruct slot = ref config.SlotStruct[i];

                if (!slot.SlotConfig.SlotEnable)
                {
                    continue;
                }

                ref var offsets = ref _state.State.SetSurfacexSlotx[i];

                using Surface src = SurfaceReader.Read(_rm, ref slot.SlotConfig, ref slot.SlotSurfaceConfig, ref offsets);

                int x1 = config.OutputConfig.TargetRectLeft;
                int y1 = config.OutputConfig.TargetRectTop;
                int x2 = config.OutputConfig.TargetRectRight + 1;
                int y2 = config.OutputConfig.TargetRectBottom + 1;

                int targetX = Math.Min(x1, x2);
                int targetY = Math.Min(y1, y2);
                int targetW = Math.Min(output.Width - targetX, Math.Abs(x2 - x1));
                int targetH = Math.Min(output.Height - targetY, Math.Abs(y2 - y1));

                Rectangle targetRect = new Rectangle(targetX, targetY, targetW, targetH);

                Blender.BlendOne(output, src, ref slot, targetRect);
            }

            SurfaceWriter.Write(_rm, output, ref config.OutputSurfaceConfig, ref _state.State.SetOutputSurface);
        }

        private T ReadIndirect<T>(uint offset) where T : unmanaged
        {
            return _gmm.Read<T>((ulong)offset << 8);
        }
    }
}
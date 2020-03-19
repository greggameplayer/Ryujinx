﻿using OpenTK.Graphics.OpenGL;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using System;

namespace Ryujinx.Graphics.OpenGL
{
    public sealed class Renderer : IRenderer
    {
        private Pipeline _pipeline;

        public IPipeline Pipeline => _pipeline;

        private readonly Counters _counters;

        private readonly Window _window;

        public IWindow Window => _window;

        internal TextureCopy TextureCopy { get; }

        public string GpuVendor { get; private set; }

        public string GpuRenderer { get; private set; }

        public string GpuVersion { get; private set; }

        public Renderer()
        {
            _pipeline = new Pipeline();

            _counters = new Counters();

            _window = new Window();

            TextureCopy = new TextureCopy();
        }

        public IShader CompileShader(ShaderProgram shader)
        {
            return new Shader(shader);
        }

        public IBuffer CreateBuffer(int size)
        {
            return new Buffer(size);
        }

        public IProgram CreateProgram(IShader[] shaders)
        {
            return new Program(shaders);
        }

        public IProgram CreateProgramFromGpuBinary(ReadOnlySpan<byte> data)
        {
            return new Program(data);
        }

        public ISampler CreateSampler(SamplerCreateInfo info)
        {
            return new Sampler(info);
        }

        public ITexture CreateTexture(TextureCreateInfo info)
        {
            return new TextureStorage(this, info).CreateDefaultView();
        }

        public Capabilities GetCapabilities()
        {
            return new Capabilities(
                HwCapabilities.SupportsAstcCompression,
                HwCapabilities.SupportsNonConstantTextureOffset,
                HwCapabilities.MaximumComputeSharedMemorySize,
                HwCapabilities.StorageBufferOffsetAlignment);
        }

        public ulong GetCounter(CounterType type)
        {
            return _counters.GetCounter(type);
        }

        public void Initialize()
        {
            GetGpuInformation();

            _counters.Initialize();
        }

        private void GetGpuInformation()
        {
            GpuVendor   = GL.GetString(StringName.Vendor);
            GpuRenderer = GL.GetString(StringName.Renderer);
            GpuVersion  = GL.GetString(StringName.Version);

            Logger.PrintInfo(LogClass.Gpu, $"{GpuVendor} {GpuRenderer} ({GpuVersion})", "GpuInformation");
        }

        public void ResetCounter(CounterType type)
        {
            _counters.ResetCounter(type);
        }

        public void Dispose()
        {
            TextureCopy.Dispose();
            _pipeline.Dispose();
            _window.Dispose();
        }
    }
}

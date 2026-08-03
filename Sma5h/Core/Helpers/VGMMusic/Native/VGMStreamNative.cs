using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace VGMMusic.Native
{
    /// <summary>
    /// Managed bindings for libvgmstream's public API.
    /// </summary>
    public static class VGMStreamNative
    {
        private const string DllName = "libvgmstream";
        private const uint SupportedApiMajor = 1;
        private const uint MinimumApiVersion = 0x01010000;

        private static bool _vgmStreamLoaded;
        private static string _libraryPath;

        public static bool VGMStreamLoaded => _vgmStreamLoaded;

        public static string LastError { get; private set; }

        static VGMStreamNative()
        {
            NativeLibrary.SetDllImportResolver(typeof(VGMStreamNative).Assembly, ResolveLibrary);
        }

        public static void SetLibraryPath(string libraryPath)
        {
            _libraryPath = libraryPath;
        }

        private static IntPtr ResolveLibrary(
            string libraryName,
            Assembly assembly,
            DllImportSearchPath? searchPath)
        {
            if (libraryName == DllName &&
                !string.IsNullOrWhiteSpace(_libraryPath) &&
                NativeLibrary.TryLoad(_libraryPath, out var handle))
                return handle;

            return IntPtr.Zero;
        }

        public static IntPtr InitVGMStream(string filename)
        {
            IntPtr streamFile = IntPtr.Zero;
            IntPtr utf8Filename = IntPtr.Zero;

            try
            {   
                //check if version is supported
                var version = libvgmstream_get_version();
                var major = version >> 24;
                if (major != SupportedApiMajor || version < MinimumApiVersion)
                {
                    LastError = $"Unsupported libvgmstream API version 0x{version:X8}. Expected version 0x{MinimumApiVersion:X8} or a compatible newer version.";
                    _vgmStreamLoaded = false;
                    return IntPtr.Zero;
                }

                //utf-8 filename to support non-unicode paths
                utf8Filename = Marshal.StringToCoTaskMemUTF8(filename);
                //create streamfile from input
                streamFile = libstreamfile_open_from_stdio(utf8Filename);
                if (streamFile == IntPtr.Zero)
                {
                    LastError = $"libvgmstream could not open '{filename}'.";
                    _vgmStreamLoaded = true;
                    return IntPtr.Zero;
                }

                var config = new LibVGMStreamConfig
                {
                    LoopCount = 1,
                    ForceSampleFormat = LibVGMStreamSampleFormat.Pcm16
                };

                //create stream
                var vgmstream = libvgmstream_create(streamFile, 0, ref config);
                _vgmStreamLoaded = true;

                if (vgmstream == IntPtr.Zero)
                    LastError = $"libvgmstream does not recognize or could not decode '{filename}'.";
                else
                    LastError = null;

                return vgmstream;
            }
            catch (Exception exception) when (
                exception is DllNotFoundException ||
                exception is BadImageFormatException ||
                exception is EntryPointNotFoundException)
            {
                _vgmStreamLoaded = false;
                LastError = exception.Message;
                return IntPtr.Zero;
            }
            finally
            {
                if (streamFile != IntPtr.Zero)
                    libstreamfile_close(streamFile);
                if (utf8Filename != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(utf8Filename);
            }
        }

        public static void ResetVGMStream(IntPtr vgmstream)
        {
            libvgmstream_reset(vgmstream);
        }

        public static void SeekVGMStream(IntPtr vgmstream, long sample)
        {
            libvgmstream_seek(vgmstream, sample);
        }

        public static void CloseVGMStream(IntPtr vgmstream)
        {
            if (vgmstream != IntPtr.Zero)
                libvgmstream_free(vgmstream);
        }

        public static int FillVGMStream(short[] buffer, int sampleCount, IntPtr vgmstream)
        {
            var result = libvgmstream_fill(vgmstream, buffer, sampleCount);
            if (result < 0)
                return 0;

            var decoder = GetNativeHandle(vgmstream).Decoder;
            if (decoder == IntPtr.Zero)
                return 0;

            return Marshal.PtrToStructure<LibVGMStreamDecoder>(decoder).BufferSamples;
        }

        public static int GetVGMStreamChannelCount(IntPtr vgmstream)
        {
            return GetFormat(vgmstream).Channels;
        }

        public static int GetVGMStreamSampleRate(IntPtr vgmstream)
        {
            return GetFormat(vgmstream).SampleRate;
        }

        public static long GetVGMStreamPlaySamples(IntPtr vgmstream)
        {
            return GetFormat(vgmstream).PlaySamples;
        }

        public static long GetVGMStreamLoopStartSample(IntPtr vgmstream)
        {
            return GetFormat(vgmstream).LoopStart;
        }

        public static long GetVGMStreamLoopEndSample(IntPtr vgmstream)
        {
            return GetFormat(vgmstream).LoopEnd;
        }

        public static long GetVGMStreamTotalSamples(IntPtr vgmstream)
        {
            return GetFormat(vgmstream).StreamSamples;
        }

        public static string[] GetVGMStreamInfo(IntPtr vgmstream)
        {
            var description = new byte[4096];
            libvgmstream_format_describe(vgmstream, description, description.Length);
            var info = System.Text.Encoding.UTF8.GetString(description).TrimEnd('\0');
            return info.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static LibVGMStream GetNativeHandle(IntPtr vgmstream)
        {
            if (vgmstream == IntPtr.Zero)
                throw new ArgumentException("The libvgmstream handle is null.", nameof(vgmstream));

            return Marshal.PtrToStructure<LibVGMStream>(vgmstream);
        }

        private static LibVGMStreamFormat GetFormat(IntPtr vgmstream)
        {
            var format = GetNativeHandle(vgmstream).Format;
            if (format == IntPtr.Zero)
                throw new InvalidOperationException("libvgmstream did not expose format information.");

            return Marshal.PtrToStructure<LibVGMStreamFormat>(format);
        }

        private enum LibVGMStreamSampleFormat
        {
            Pcm16 = 1,
            Pcm24 = 2,
            Pcm32 = 3,
            Float = 4
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LibVGMStream
        {
            public IntPtr Private;
            public IntPtr Format;
            public IntPtr Decoder;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LibVGMStreamDecoder
        {
            public IntPtr Buffer;
            public int BufferSamples;
            public int BufferBytes;

            [MarshalAs(UnmanagedType.I1)]
            public bool Done;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LibVGMStreamFormat
        {
            public int Channels;
            public int SampleRate;
            public LibVGMStreamSampleFormat SampleFormat;
            public int SampleSize;
            public uint ChannelLayout;
            public int SubsongIndex;
            public int SubsongCount;
            public int InputChannels;
            public long StreamSamples;
            public long LoopStart;
            public long LoopEnd;

            [MarshalAs(UnmanagedType.I1)]
            public bool LoopFlag;

            [MarshalAs(UnmanagedType.I1)]
            public bool PlayForever;

            public long PlaySamples;
            public int StreamBitrate;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LibVGMStreamConfig
        {
            [MarshalAs(UnmanagedType.I1)]
            public bool DisableConfigOverride;

            [MarshalAs(UnmanagedType.I1)]
            public bool AllowPlayForever;

            [MarshalAs(UnmanagedType.I1)]
            public bool PlayForever;

            [MarshalAs(UnmanagedType.I1)]
            public bool IgnoreLoop;

            [MarshalAs(UnmanagedType.I1)]
            public bool ForceLoop;

            [MarshalAs(UnmanagedType.I1)]
            public bool ReallyForceLoop;

            [MarshalAs(UnmanagedType.I1)]
            public bool IgnoreFade;

            public double LoopCount;
            public double FadeTime;
            public double FadeDelay;
            public int StereoTrack;
            public int AutoDownmixChannels;
            public LibVGMStreamSampleFormat ForceSampleFormat;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint libvgmstream_get_version();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr libstreamfile_open_from_stdio(IntPtr filename);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libstreamfile_close(IntPtr streamFile);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr libvgmstream_create(
            IntPtr streamFile,
            int subsong,
            ref LibVGMStreamConfig config);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libvgmstream_free(IntPtr vgmstream);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int libvgmstream_fill(
            IntPtr vgmstream,
            [Out] short[] buffer,
            int sampleCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libvgmstream_seek(IntPtr vgmstream, long sample);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void libvgmstream_reset(IntPtr vgmstream);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int libvgmstream_format_describe(
            IntPtr vgmstream,
            [Out] byte[] description,
            int descriptionLength);
    }
}

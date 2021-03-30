using NUnit.Framework;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;

namespace resvg_tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
            if (!System.IO.Directory.Exists("pngs"))
                System.IO.Directory.CreateDirectory("pngs");
        }

        [Test, TestCaseSource(nameof(SvgTestCaseData))]
        public void TestSvgToImage(string svgPath)
        {
            string svg = System.IO.File.ReadAllText(svgPath);
            var bitmap = Resvg.BitmapFromSvg(svg, 32, 32);
            string filename = System.IO.Path.GetFileNameWithoutExtension(svgPath);
            bitmap.Save($"pngs/{filename}.png");

            // we probably need a better comparison when running on multiple platforms
            byte[] base_bytes = System.IO.File.ReadAllBytes($"result_pngs/{filename}.png");
            byte[] written_bytes = System.IO.File.ReadAllBytes($"pngs/{filename}.png");
            Assert.AreEqual(base_bytes, written_bytes);
        }

        private static IEnumerable<TestCaseData> SvgTestCaseData
        {
            get
            {
                foreach( var svg in System.IO.Directory.EnumerateFiles("svgs"))
                {
                    yield return new TestCaseData(svg);
                }
            }
        }
    }


    [System.Security.SuppressUnmanagedCodeSecurity]
    static class UnsafeNativeMethods
    {
        #region resvg
        internal const string resvgDllPath = "resvg_rhino";

        [DllImport(resvgDllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr resvg_options_create();

        [DllImport(resvgDllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void resvg_options_destroy(IntPtr opt);

        [DllImport(resvgDllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int resvg_parse_tree_from_data([MarshalAs(UnmanagedType.LPStr)] string data,
          IntPtr len,
          IntPtr options, //const resvg_options* opt
          ref IntPtr renderTree); //resvg_render_tree **tree


        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 8)]
        internal struct resvg_fit_to
        {
            public byte _fitToType;
            public float _value;
        }

        [DllImport(resvgDllPath, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void resvg_render(IntPtr renderTree,
                      resvg_fit_to fit_to,
                      uint width,
                      uint height,
                      IntPtr pixmap);
        #endregion
    }

    static class Resvg
    {
        public static byte[] PixelsFromSvg(string svg, int width, int height)
        {
            if (string.IsNullOrWhiteSpace(svg) || width < 1 || height < 1)
                return null;

            IntPtr options = UnsafeNativeMethods.resvg_options_create();
            IntPtr renderTree = IntPtr.Zero;

            UnsafeNativeMethods.resvg_parse_tree_from_data(svg, new IntPtr(svg.Length), options, ref renderTree);

            byte[] pixmap = new byte[width * height * 4];
            var fitto = new UnsafeNativeMethods.resvg_fit_to();
            // Use Fitto width option. Seems to work for the tests I have run so far.
            fitto._fitToType = 1;
            fitto._value = (float)width;
            GCHandle pinnedArray = GCHandle.Alloc(pixmap, GCHandleType.Pinned);
            IntPtr pBytes = pinnedArray.AddrOfPinnedObject();

            UnsafeNativeMethods.resvg_render(renderTree, fitto, (uint)width, (uint)height, pBytes);
            UnsafeNativeMethods.resvg_options_destroy(options);
            pinnedArray.Free();
            return pixmap;
        }

        public static Bitmap BitmapFromSvg(string svg, int width, int height)
        {
            Bitmap sizedBmp = null;
            byte[] pixmap = PixelsFromSvg(svg, width, height);

            if (pixmap != null && pixmap.Length > 0)
            {
                for (int i = 0; i < pixmap.Length; i += 4)
                {
                    byte r = pixmap[i];
                    //byte g = pixmap[i + 1];
                    byte b = pixmap[i + 2];
                    //byte a = pixmap[i + 3];

                    pixmap[i] = b;
                    //pixmap[i + 1] = g;
                    pixmap[i + 2] = r;
                    //pixmap[i + 3] = a;
                }

                int stride = 4 * width;
                var pinnedArray = System.Runtime.InteropServices.GCHandle.Alloc(pixmap, System.Runtime.InteropServices.GCHandleType.Pinned);
                IntPtr pBytes = pinnedArray.AddrOfPinnedObject();
                var bmp = new System.Drawing.Bitmap(width, height, stride, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, pBytes);
                // create a copy as bmp will have invalid data once the pinned byte[] is released
                sizedBmp = new System.Drawing.Bitmap(bmp, width, height);
                bmp.Dispose();
                pinnedArray.Free();
            }
            return sizedBmp;
        }
    }
}

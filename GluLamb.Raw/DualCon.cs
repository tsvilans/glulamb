using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace GluLamb.Raw
{
    /*
   [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DualConInput
    {
        public int[] mloop;
        public float[] co;
        public int co_stride;
        public int totco;
        public int[] corner_tris;
        public int tri_stride;
        public int tottri;
        public int loop_stride;
        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 3 * 32)]
        //public float[] min;
        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 3 * 32)]
        //public float[] max;
        public float minx, miny, minz;
        public float maxx, maxy, maxz;
    }
    */

    public class DualConOutput
    {
        public float[][] Vertices;
        public int[][] Quads;
        public int currentVert, currentQuad;

        public DualConOutput(int numVertices, int numFaces)
        {
            Vertices = new float[numVertices][];
            Quads = new int[numFaces][];
            currentVert = 0;
            currentQuad = 0;
        }
    }


    public class DualCon
    {
        private unsafe struct DualConInput
        {
            public int* mloop;

            public float* co;
            public int co_stride;
            public int totco;

            public int* corner_tris;
            public int tri_stride;
            public int tottri;

            public int loop_stride;

            public fixed float min[3], max[3];
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr AllocateOutputDelegate(int nv, int nf);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate void AddVertexDelegate(IntPtr output, float* data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public unsafe delegate void AddFaceDelegate(IntPtr output, int* data);

        [DllImport(Api.DualConPath, SetLastError = false, CallingConvention = CallingConvention.Cdecl)]
        private unsafe static extern IntPtr dualcon(
            DualConInput* input, 
            AllocateOutputDelegate alloc_output, 
            AddVertexDelegate add_vert, AddFaceDelegate add_quad,
            int flags, int mode, float threshold,
            float hermite_num, float scale, int depth);

        public DualConOutput? Output;
        GCHandle Handle;

        public int Flags = 0, Mode = 2, Depth = 2;
        public float Threshold = 1.0f, HermiteNumber = 1.0f, Scale = 0.9f;

        public DualCon()
        {
        }

        public IntPtr AllocateOutput(int numVertices, int numFaces)
        {
            Output = new DualConOutput(numVertices, numFaces);
            Console.WriteLine($"Allocating {numVertices} verts and {numFaces} faces.");

            return IntPtr.Zero;

            Handle = GCHandle.Alloc(Output);
            return GCHandle.ToIntPtr(Handle);
            
            IntPtr parameter = (IntPtr)Handle;
            // call WinAPi and pass the parameter here
            // then free the handle when not needed:
            Handle.Free();

            // back to object (in callback function):
            GCHandle handle2 = (GCHandle)parameter;
            List<string> list2 = (handle2.Target as List<string>);
            list2.Add("hello world");
        }

        public unsafe void AddVertex(IntPtr output, float* vertices)
        {
            Output.Vertices[Output.currentVert] = new float[] { vertices[0], vertices[1], vertices[2] };
            //Console.WriteLine($"Adding vertex {vertices[0]:0.000}, {vertices[1]:0.000}, {vertices[2]:0.000}");
            Output.currentVert++;
        }

        public unsafe void AddQuad(IntPtr output, int* quad)
        {
            //Console.WriteLine($"Adding quad {quad[0]}, {quad[1]}, {quad[2]}, {quad[3]}");
            Output.Quads[Output.currentQuad] = new int[] { quad[0], quad[1], quad[2], quad[3] };
            Output.currentQuad++;
        }

        public void Remesh(float[] vertices, int[] faces)
        {
            //DualConOutput output = null;

            unsafe
            {
                fixed (float* vertPointer = &vertices[0])
                {
                    fixed (int* facePointer = &faces[0])
                    {

                        var input = new DualConInput()
                        {
                            totco = vertices.Length / 3,
                            co = vertPointer,
                            co_stride = sizeof(float) * 3,
                            corner_tris = facePointer,
                            tottri = faces.Length / 3,
                            tri_stride = sizeof(int) * 3,
                            mloop = facePointer,
                            loop_stride = sizeof(int)
                        };

                        var min = new float[3] { float.MaxValue, float.MaxValue, float.MaxValue };
                        var max = new float[3] { float.MinValue, float.MinValue, float.MinValue };

                        for(int i = 0; i < vertices.Length; i+=3)
                        {
                            for (int j = 0; j < 3; ++j)
                            {
                                min[j] = Math.Min(min[j], vertices[i + j]);
                                max[j] = Math.Max(max[j], vertices[i + j]);
                            }
                        }

                        input.min[0] = min[0]; input.min[1] = min[1]; input.min[2] = min[2];
                        input.max[0] = max[0]; input.max[1] = max[1]; input.max[2] = max[2];
                        var ptr = dualcon(&input, AllocateOutput, AddVertex, AddQuad, Flags, Mode, Threshold, HermiteNumber, Scale, Depth);

                        // back to object (in callback function):
                        //GCHandle handle = (GCHandle)ptr;
                        //output = handle.Target as DualConOutput;
                        //handle.Free();
                        //Handle.Free();
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using Rhino.Geometry;

namespace GluLamb.RawLam
{
    public class LamellaPerformance
    {
        public int Id;
        public Mesh Mesh;
        public Dictionary<string, double[]> Layers;

        public LamellaPerformance(Mesh mesh, int id = -1)
        {
            Mesh = mesh;
            Layers = new Dictionary<string, double[]>();
        }

        public void AddLayer(string name)
        {
            Layers.Add(name, new double[Mesh.Vertices.Count]);
        }

        public byte[] Serialize()
        {
            /* Calculate size of data */
            int N = 0;
            N += sizeof(int); // lamella id

            // Mesh
            N += sizeof(int); // number of mesh vertices
            N += sizeof(float) * 3 * Mesh.Vertices.Count; // mesh vertices
            N += sizeof(int); // number of mesh faces
            N += sizeof(int) * 4 * Mesh.Faces.Count; // mesh faces

            N += sizeof(int); // number of layers

            foreach (string key in Layers.Keys)
            {
                N += sizeof(int); // length of layer name
                N += key.Length; // layer name
                N += sizeof(int); // length of layer values (should be the same as vertices)

                N += sizeof(double) * Layers[key].Length; // layer values
            }


            var data = new byte[N];
            var index = 0;

            /* Convert data */

            // Id
            Buffer.BlockCopy(BitConverter.GetBytes(Id), 0, data, index, sizeof(int));
            index += sizeof(int);

            // Mesh
            Buffer.BlockCopy(BitConverter.GetBytes(Mesh.Vertices.Count), 0, data, index, sizeof(int));
            index += sizeof(int);

            Buffer.BlockCopy(Mesh.Vertices.ToFloatArray(), 0, data, index, Mesh.Vertices.Count * 3 * sizeof(float));
            index += Mesh.Vertices.Count * 3 * sizeof(float);

            Buffer.BlockCopy(BitConverter.GetBytes(Mesh.Faces.Count), 0, data, index, sizeof(int));
            index += sizeof(int);

            Buffer.BlockCopy(Mesh.Faces.ToIntArray(false), 0, data, index, Mesh.Faces.Count * 4 * sizeof(int));
            index += Mesh.Faces.Count * 4 * sizeof(int);

            // Layers
            Buffer.BlockCopy(BitConverter.GetBytes(Layers.Keys.Count), 0, data, index, sizeof(int));
            index += sizeof(int);

            foreach (string key in Layers.Keys)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(key.Length), 0, data, index, sizeof(int));
                index += sizeof(int);

                Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(key), 0, data, index, key.Length);
                index += key.Length;

                Buffer.BlockCopy(BitConverter.GetBytes(Layers[key].Length), 0, data, index, sizeof(int));
                index += sizeof(int);

                Buffer.BlockCopy(Layers[key], 0, data, index, sizeof(double) * Layers[key].Length);
                index += sizeof(double) * Layers[key].Length;
            }

            return data;
        }

        public static LamellaPerformance Deserialize(byte[] data)
        {
            Mesh mesh = new Mesh();
            int index = 0;

            int id = BitConverter.ToInt32(data, index); index += sizeof(int);
            int nVerts = BitConverter.ToInt32(data, index); index += sizeof(int);

            var vertArray = new float[nVerts * 3];
            Buffer.BlockCopy(data, index, vertArray, 0, nVerts * 3 * sizeof(float));
            index += nVerts * 3 * sizeof(float);

            for (int i = 0; i < nVerts; ++i)
                mesh.Vertices.Add(vertArray[i * 3], vertArray[i * 3 + 1], vertArray[i * 3 + 2]);

            int nFaces = BitConverter.ToInt32(data, index); index += sizeof(int);

            var faceArray = new int[nFaces * 4];
            Buffer.BlockCopy(data, index, faceArray, 0, nFaces * 4 * sizeof(int));
            index += nFaces * 4 * sizeof(int);

            for (int i = 0; i < nFaces; ++i)
                mesh.Faces.AddFace(
                  faceArray[i * 4],
                  faceArray[i * 4 + 1],
                  faceArray[i * 4 + 2],
                  faceArray[i * 4 + 3]);

            var lp = new LamellaPerformance(mesh, id);

            int nLayers = BitConverter.ToInt32(data, index); index += sizeof(int);

            for (int i = 0; i < nLayers; ++i)
            {
                int nameLength = BitConverter.ToInt32(data, index); index += sizeof(int);
                var nameBytes = new byte[nameLength];
                Buffer.BlockCopy(data, index, nameBytes, 0, nameLength); index += nameLength;
                string layer_name = System.Text.Encoding.UTF8.GetString(nameBytes);

                int layerLength = BitConverter.ToInt32(data, index); index += sizeof(int);
                var layerData = new double[layerLength];
                Buffer.BlockCopy(data, index, layerData, 0, layerLength * sizeof(double));
                index += layerLength * sizeof(double);

                lp.Layers.Add(layer_name, layerData);
            }

            return lp;
        }

        public static byte[] SerializeMany(IEnumerable<LamellaPerformance> lps)
        {
            var Nbytes = 0;
            var datas = new List<byte[]>();
            foreach(var lp in lps)
            {
                Nbytes += sizeof(int);
                var lpdata = lp.Serialize();
                Nbytes += lpdata.Length;

                datas.Add(lpdata);
            }

            var data = new byte[Nbytes];

            int index = 0;
            Buffer.BlockCopy(BitConverter.GetBytes(Nbytes), 0, data, index, sizeof(int));
            index += sizeof(int);

            foreach (var lpdata in datas)
            {
                Buffer.BlockCopy(lpdata, 0, data, index, lpdata.Length);
                index += lpdata.Length;
            }

            return data;
        }

        public static List<LamellaPerformance> DeserializeMany(byte[] data)
        {
            var lps = new List<LamellaPerformance>();

            int index = 0;
            var N = BitConverter.ToInt32(data, index);

            for (int i = 0; i < N; ++i)
            {
                var lpLength = BitConverter.ToInt32(data, index);
                index += sizeof(int);

                var lpdata = new byte[lpLength];
                Buffer.BlockCopy(data, index, lpdata, 0, lpLength);

                var lp = LamellaPerformance.Deserialize(lpdata);

                lps.Add(lp);
            }

            return lps;
        }
    }
}

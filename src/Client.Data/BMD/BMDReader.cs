using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Collections.Specialized.BitVector32;

namespace Client.Data.BMD
{
    public class BMDReader : BaseReader<BMD>
    {
        protected override BMD Read(byte[] buffer)
        {
            if (buffer.Length < 8)
                throw new FileLoadException("Invalid size.");

            var fileType = Encoding.ASCII.GetString(buffer, 0, 3);

            if (fileType != "BMD")
                throw new FileLoadException($"Invalid file type. Expected BMD and Received {fileType}.");

            var version = buffer[3];

            if (version == 12)
            {
                var size = BitConverter.ToInt32(buffer, 4);
                var enc = new byte[size];
                Array.Copy(buffer, 8, enc, 0, size);
                var dec = FileCryptor.Decrypt(enc);
                Array.Copy(dec, 0, buffer, 4, size);
            }
            else if (version == 15)
            {
                var size = BitConverter.ToInt32(buffer, 4);
                var enc = new byte[size];
                Array.Copy(buffer, 8, enc, 0, size);
                var dec = LEACrypto.Decrypt(enc);
                Array.Copy(dec, 0, buffer, 4, size);
            }

            using var ms = new MemoryStream(buffer);
            using var br = new BinaryReader(ms);

            ms.Seek(4, SeekOrigin.Begin);

            var name = br.ReadString(32);
            var meshes = new BMDTextureMesh[br.ReadUInt16()];
            var bones = new BMDTextureBone[br.ReadUInt16()];
            var actions = new BMDTextureAction[br.ReadUInt16()];

            for (var i = 0; i < meshes.Length; i++)
            {
                var numVertices = br.ReadInt16();
                var numNormals = br.ReadInt16();
                var numTexCoords = br.ReadInt16();
                var numTriangles = br.ReadInt16();
                var texture = br.ReadInt16();

                var vertices = br.ReadStructArray<BMDTextureVertex>(numVertices);
                var normals = br.ReadStructArray<BMDTextureNormal>(numNormals);
                var textCoords = br.ReadStructArray<BMDTexCoord>(numTexCoords);
                var triangles = br.ReadStructArray<BMDTriangle>(numTriangles);
                var texturePath = br.ReadString(32);

                meshes[i] = new BMDTextureMesh()
                {
                    Texture = texture,
                    Vertices = vertices,
                    Normals = normals,
                    TexCoords = textCoords,
                    Triangles = triangles,
                    TexturePath = texturePath,
                };
            }

            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i] = new BMDTextureAction()
                {
                    NumAnimationKeys = br.ReadInt16(),
                    LockPositions = br.ReadBoolean(),
                };

                if (action.LockPositions)
                {
                    action.Positions = br.ReadStructArray<Vector3>(action.NumAnimationKeys);
                }
            }

            for (var i = 0; i < bones.Length; i++)
            {
                var dummy = br.ReadBoolean();

                if (dummy)
                {
                    bones[i] = BMDTextureBone.Dummy;
                }
                else
                {
                    var bone = bones[i] = new BMDTextureBone()
                    {
                        Name = br.ReadString(32),
                        Parent = br.ReadInt16(),
                        Matrixes = new BMDBoneMatrix[actions.Length]
                    };

                    for (var m = 0; m < bone.Matrixes.Length; m++)
                    {
                        var action = actions[m];
                        bone.Matrixes[m] = new BMDBoneMatrix()
                        {
                            Position = br.ReadStructArray<Vector3>(action.NumAnimationKeys),
                            Rotation = br.ReadStructArray<Vector3>(action.NumAnimationKeys)
                        };

                        // precalculate quaternion
                        bone.Matrixes[m].Quaternion = new Quaternion[action.NumAnimationKeys];
                        for (var r = 0; r < action.NumAnimationKeys; r++)
                            bone.Matrixes[m].Quaternion[r] = MathUtils.AngleQuaternion(bone.Matrixes[m].Rotation[r]);
                    }
                }
            }

            return new BMD
            {
                Version = version,
                Name = name,
                Meshes = meshes,
                Bones = bones,
                Actions = actions,
            };
        }
    }
}

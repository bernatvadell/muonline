using System.Numerics;
using System.Text;

namespace Client.Data.BMD
{
    public class BMDReader : BaseReader<BMD>
    {
        private const string EXPECTED_FILE_TYPE = "BMD";
        private const int MINIMAL_BUFFER_SIZE = 8;
        private const int STRING_LENGTH = 32;

        /// <summary>
        /// Public method to read BMD from byte array (for embedded resources)
        /// </summary>
        public BMD ReadFromBuffer(byte[] buffer) => Read(buffer);

        protected override BMD Read(byte[] buffer)
        {
            ValidateBuffer(buffer);
            
            var version = buffer[3];
            DecryptBufferIfNeeded(buffer, version);

            using var br = new BinaryReader(new MemoryStream(buffer));
            br.BaseStream.Seek(4, SeekOrigin.Begin);

            var name = br.ReadString(STRING_LENGTH);
            var meshCount = br.ReadUInt16();
            var boneCount = br.ReadUInt16();
            var actionCount = br.ReadUInt16();

            var meshes = ReadMeshes(br, meshCount);
            var actions = ReadActions(br, actionCount);
            var bones = ReadBones(br, boneCount, actions);

            return new BMD
            {
                Version = version,
                Name = name,
                Meshes = meshes,
                Actions = actions,
                Bones = bones
            };
        }

        private void ValidateBuffer(byte[] buffer)
        {
            if (buffer.Length < MINIMAL_BUFFER_SIZE)
                throw new FileLoadException("Invalid size.");

            var fileType = Encoding.ASCII.GetString(buffer, 0, 3);
            if (fileType != EXPECTED_FILE_TYPE)
                throw new FileLoadException($"Invalid file type. Expected {EXPECTED_FILE_TYPE} and Received {fileType}.");
        }

        private void DecryptBufferIfNeeded(byte[] buffer, byte version)
        {
            if (version is not 12 and not 15) return;

            var size = BitConverter.ToInt32(buffer, 4);
            var enc = new byte[size];
            Array.Copy(buffer, 8, enc, 0, size);
            
            var dec = version == 12 
                ? FileCryptor.Decrypt(enc) 
                : LEACrypto.Decrypt(enc);
            
            Array.Copy(dec, 0, buffer, 4, size);
        }

        private BMDTextureMesh[] ReadMeshes(BinaryReader br, int meshCount)
        {
            var meshes = new BMDTextureMesh[meshCount];
            
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
                var texturePath = br.ReadString(STRING_LENGTH);

                meshes[i] = new BMDTextureMesh
                {
                    Texture = texture,
                    Vertices = vertices,
                    Normals = normals,
                    TexCoords = textCoords,
                    Triangles = triangles,
                    TexturePath = texturePath
                };
            }

            return meshes;
        }

        private BMDTextureAction[] ReadActions(BinaryReader br, int actionCount)
        {
            var actions = new BMDTextureAction[actionCount];

            for (var i = 0; i < actions.Length; i++)
            {
                actions[i] = new BMDTextureAction
                {
                    NumAnimationKeys = br.ReadInt16(),
                    LockPositions = br.ReadBoolean()
                };

                if (actions[i].LockPositions)
                {
                    actions[i].Positions = br.ReadStructArray<Vector3>(actions[i].NumAnimationKeys);
                }
            }

            return actions;
        }

        private BMDTextureBone[] ReadBones(BinaryReader br, int boneCount, BMDTextureAction[] actions)
        {
            var bones = new BMDTextureBone[boneCount];

            for (var i = 0; i < bones.Length; i++)
            {
                if (br.ReadBoolean())
                {
                    bones[i] = BMDTextureBone.Dummy;
                }
                else
                {
                    bones[i] = new BMDTextureBone
                    {
                        Name = br.ReadString(STRING_LENGTH),
                        Parent = br.ReadInt16(),
                        Matrixes = new BMDBoneMatrix[actions.Length]
                    };

                    for (var m = 0; m < actions.Length; m++)
                    {
                        var matrix = new BMDBoneMatrix
                        {
                            Position = br.ReadStructArray<Vector3>(actions[m].NumAnimationKeys),
                            Rotation = br.ReadStructArray<Vector3>(actions[m].NumAnimationKeys),
                            Quaternion = new Quaternion[actions[m].NumAnimationKeys]
                        };

                        for (var r = 0; r < actions[m].NumAnimationKeys; r++)
                        {
                            matrix.Quaternion[r] = MathUtils.AngleQuaternion(matrix.Rotation[r]);
                        }

                        bones[i].Matrixes[m] = matrix;
                    }
                }
            }

            return bones; 
        }
    }
}
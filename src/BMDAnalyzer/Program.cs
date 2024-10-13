// See https://aka.ms/new-console-template for more information
using Client.Data.BMD;
using System.Text;

Console.WriteLine("Hello, World!");

//var buffer = File.ReadAllBytes("C:\\Games\\MU_Red_1_20_61_Full\\Data\\Player\\player.bmd");
//var encLength = BitConverter.ToInt32(buffer, 4);
//var encData = new byte[encLength];
//Array.Copy(buffer, 8, encData, 0, encData.Length);

//var lea = new LEA.Symmetric.Lea.Ecb();

//byte[] mk =
//[
//    0xcc,
//    0x50,
//    0x45,
//    0x13,
//    0xc2,
//    0xa6,
//    0x57,
//    0x4e,
//    0xd6,
//    0x9a,
//    0x45,
//    0x89,
//    0xbf,
//    0x2f,
//    0xbc,
//    0xd9,
//    0x39,
//    0xb3,
//    0xb3,
//    0xbd,
//    0x50,
//    0xbd,
//    0xcc,
//    0xb6,
//    0x85,
//    0x46,
//    0xd1,
//    0xd6,
//    0x16,
//    0x54,
//    0xe0,
//    0x87,
//];

//lea.Init(LEA.BlockCipher.Mode.DECRYPT, mk);
//var buff = lea.DoFinal(encData);
//var fileName = Encoding.UTF8.GetString(buff, 0, 32);

var reader = new BMDReader();

// var original = await reader.Load("C:\\Games\\MU Client 1.04d - Season 6E3\\Data\\Player\\player.bmd");
var season20Enc = await reader.Load("C:\\Games\\MU_Red_1_20_61_Full\\Data\\Player\\player.bmd");
// var season20 = await reader.Load("C:\\Games\\player.bmd");
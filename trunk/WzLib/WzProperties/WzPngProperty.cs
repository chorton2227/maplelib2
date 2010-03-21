﻿/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010 Snow and haha01haha01
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/
//uncomment the line below to create a space-time tradeoff (saving RAM by wasting more CPU cycles)
//only works if PNGS is defined
#define SPACETIME

//uncomment to enable memory-saving by reading PNGs from hard disk
//instead of storing them on memory
#define PNGS

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using MapleLib.WzLib.Util;

namespace MapleLib.WzLib.WzProperties
{
	/// <summary>
	/// A property that contains the information for a bitmap
	/// </summary>
	public class WzPngProperty : IWzImageProperty
	{
		#region Fields
		internal int width, height, format, format2;
		internal byte[] compressedBytes;
		internal Bitmap png;
		internal bool isNew = false;
		internal IWzObject parent;
		internal WzImage imgParent;
#if PNGS
        internal WzBinaryReader wzReader;
        internal long offs;
#endif
		#endregion

		#region Inherited Members
        public override IWzImageProperty DeepClone()
        {
            WzPngProperty clone = (WzPngProperty)MemberwiseClone();
            return clone;
        }

		public override object WzValue { get { return PNG; } }
		/// <summary>
		/// The parent of the object
		/// </summary>
		public override IWzObject Parent { get { return parent; } internal set { parent = value; } }
		/// <summary>
		/// The image that this property is contained in
		/// </summary>
		public override WzImage ParentImage { get { return imgParent; } internal set { imgParent = value; } }
		/// <summary>
		/// The name of the property
		/// </summary>
		public override string Name { get { return "PNG"; } set { } }
		/// <summary>
		/// The WzPropertyType of the property
		/// </summary>
		public override WzPropertyType PropertyType { get { return WzPropertyType.PNG; } }
		public override void WriteValue(WzBinaryWriter writer)
		{
			throw new NotImplementedException("Cannot write a PngProperty");
		}
		/// <summary>
		/// Disposes the object
		/// </summary>
		public override void Dispose()
		{
			compressedBytes = null;
			if (png != null)
			{
				png.Dispose();
				png = null;
			}
		}
		#endregion

		#region Custom Members
		/// <summary>
		/// The width of the bitmap
		/// </summary>
		public int Width { get { return width; } set { width = value; } }
		/// <summary>
		/// The height of the bitmap
		/// </summary>
		public int Height { get { return height; } set { height = value; } }
		/// <summary>
		/// The format of the bitmap
		/// </summary>
		public int Format { get { return format + format2; } set { format = value; format2 = 0; } }
		/// <summary>
		/// The actual bitmap
		/// </summary>
		public Bitmap PNG
		{
			get
			{
                if (png == null)
                {
#if PNGS
                    long pos = wzReader.BaseStream.Position;
                    wzReader.BaseStream.Position = offs;
                    int len = wzReader.ReadInt32() - 1;
                    wzReader.BaseStream.Position += 1;
                    if (len > 0)
                        compressedBytes = wzReader.ReadBytes(len);
#endif
                    ParsePng();
#if PNGS
                    wzReader.BaseStream.Position = pos;
#endif
#if SPACETIME
                    Bitmap pngImage = png;
                    png = null;
                    compressedBytes = null;
                    return pngImage;
#endif
                }
				return png;
			}
			set
			{
				png = value;
				CompressPng(value);
			}
		}
		internal byte[] CompressedBytes
        {
            get 
            {
#if PNGS
                if (compressedBytes == null)
                {
                    long pos = wzReader.BaseStream.Position;
                    wzReader.BaseStream.Position = offs;
                    int len = wzReader.ReadInt32() - 1;
                    wzReader.BaseStream.Position += 1;
                    if (len > 0)
                        compressedBytes = wzReader.ReadBytes(len);
                    wzReader.BaseStream.Position = pos;
#if SPACETIME
                    byte[] compBytes = compressedBytes;
                    compressedBytes = null;
                    return compBytes;
#endif
                }
#endif
                return compressedBytes; 
            } 
        }
		/// <summary>
		/// Creates a blank WzPngProperty
		/// </summary>
		public WzPngProperty() { }
		internal WzPngProperty(WzBinaryReader reader)
		{
			// Read compressed bytes
			width = reader.ReadCompressedInt();
			height = reader.ReadCompressedInt();
			format = reader.ReadCompressedInt();
			format2 = reader.ReadByte();
			reader.BaseStream.Position += 4;
#if PNGS
            offs = reader.BaseStream.Position;
#endif
			int len = reader.ReadInt32() - 1;
			reader.BaseStream.Position += 1;

            if (len > 0)
#if PNGS
                reader.BaseStream.Position += len;
#else
                compressedBytes = reader.ReadBytes(len);
#endif
#if PNGS
            wzReader = reader;
#endif
		}
		#endregion

		#region Parsing Methods
		internal byte[] Decompress(byte[] compressedBuffer, int decompressedSize)
		{
			MemoryStream memStream = new MemoryStream();
			memStream.Write(compressedBuffer, 2, compressedBuffer.Length - 2);
			byte[] buffer = new byte[decompressedSize];
			memStream.Position = 0;
			DeflateStream zip = new DeflateStream(memStream, CompressionMode.Decompress);
			zip.Read(buffer, 0, buffer.Length);
			zip.Close();
			zip.Dispose();
			memStream.Close();
			memStream.Dispose();
			return buffer;
		}
		internal byte[] Compress(byte[] decompressedBuffer)
		{
			MemoryStream memStream = new MemoryStream();
			DeflateStream zip = new DeflateStream(memStream, CompressionMode.Compress, true);
			zip.Write(decompressedBuffer, 0, decompressedBuffer.Length);
			zip.Close();
			memStream.Position = 0;
			byte[] buffer = new byte[memStream.Length + 2];
			Console.WriteLine(BitConverter.ToString(memStream.ToArray()));
			memStream.Read(buffer, 2, buffer.Length - 2);
			memStream.Close();
			memStream.Dispose();
			zip.Dispose();
			System.Buffer.BlockCopy(new byte[] { 0x78, 0x9C }, 0, buffer, 0, 2);
			return buffer;
		}
		internal void ParsePng()
		{
			DeflateStream zlib;
			int uncompressedSize = 0;
			int x = 0, y = 0, b = 0, g = 0;
			Bitmap bmp = null;
			BitmapData bmpData;
			byte[] decBuf;

			BinaryReader reader = new BinaryReader(new MemoryStream(compressedBytes));
			ushort header = reader.ReadUInt16();
			if (header == 0x9C78)
			{
				zlib = new DeflateStream(reader.BaseStream, CompressionMode.Decompress);
			}
			else
			{
				reader.BaseStream.Position -= 2;
				MemoryStream dataStream = new MemoryStream();
				int blocksize = 0;
				int endOfPng = compressedBytes.Length;

				while (reader.BaseStream.Position < endOfPng)
				{
					blocksize = reader.ReadInt32();
					for (int i = 0; i < blocksize; i++)
					{
						dataStream.WriteByte((byte)(reader.ReadByte() ^ imgParent.reader.WzKey[i]));
					}
				}
				dataStream.Position = 2;
				zlib = new DeflateStream(dataStream, CompressionMode.Decompress);
			}

			switch (format + format2)
			{
				case 1:
					bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
					bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
					uncompressedSize = width * height * 2;
					decBuf = new byte[uncompressedSize];
					zlib.Read(decBuf, 0, uncompressedSize);
					byte[] argb = new Byte[uncompressedSize * 2];
					for (int i = 0; i < uncompressedSize; i++)
					{
						b = decBuf[i] & 0x0F; b |= (b << 4); argb[i * 2] = (byte)b;
						g = decBuf[i] & 0xF0; g |= (g >> 4); argb[i * 2 + 1] = (byte)g;
					}
					Marshal.Copy(argb, 0, bmpData.Scan0, argb.Length);
					bmp.UnlockBits(bmpData);
					break;
				case 2:
					bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
					bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
					uncompressedSize = width * height * 4;
					decBuf = new byte[uncompressedSize];
					zlib.Read(decBuf, 0, uncompressedSize);
					Marshal.Copy(decBuf, 0, bmpData.Scan0, decBuf.Length);
					bmp.UnlockBits(bmpData);
					break;
				case 513:
					bmp = new Bitmap(width, height, PixelFormat.Format16bppRgb565);
					bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb565);
					uncompressedSize = width * height * 2;
					decBuf = new byte[uncompressedSize];
					zlib.Read(decBuf, 0, uncompressedSize);
					Marshal.Copy(decBuf, 0, bmpData.Scan0, decBuf.Length);
					bmp.UnlockBits(bmpData);
					break;

				case 517:
					bmp = new Bitmap(width, height);
					uncompressedSize = width * height / 128;
					decBuf = new byte[uncompressedSize];
					zlib.Read(decBuf, 0, uncompressedSize);
					byte iB = 0;
					for (int i = 0; i < uncompressedSize; i++)
					{
						for (byte j = 0; j < 8; j++)
						{
							iB = Convert.ToByte(((decBuf[i] & (0x01 << (7 - j))) >> (7 - j)) * 0xFF);
							for (int k = 0; k < 16; k++)
							{
								if (x == width) { x = 0; y++; }
								bmp.SetPixel(x, y, Color.FromArgb(0xFF, iB, iB, iB));
								x++;
							}
						}
					}
					break;
			}
			png = bmp;
		}
		internal void CompressPng(Bitmap bmp)
		{
			//Console.WriteLine("a"); why was that here anyway...
			byte[] buf = new byte[bmp.Width * bmp.Height * 8];
			format = 2;
			format2 = 0;
			width = bmp.Width;
			height = bmp.Height;

			int curPos = 0;
			for (int i = 0; i < height; i++)
				for (int j = 0; j < width; j++)
				{
					Color curPixel = bmp.GetPixel(j, i);
					buf[curPos] = curPixel.B;
					buf[curPos + 1] = curPixel.G;
					buf[curPos + 2] = curPixel.R;
					buf[curPos + 3] = curPixel.A;
					curPos += 4;
				}
			compressedBytes = Compress(buf);
			if (isNew)
			{
				MemoryStream memStream = new MemoryStream();
				WzBinaryWriter writer = new WzBinaryWriter(memStream, WzTool.GetIvByMapleVersion(WzMapleVersion.GMS));
				writer.Write(2);
				for (int i = 0; i < 2; i++)
				{
					writer.Write((byte)(compressedBytes[i] ^ writer.WzKey[i]));
				}
				writer.Write(compressedBytes.Length - 2);
				for (int i = 2; i < compressedBytes.Length; i++)
					writer.Write((byte)(compressedBytes[i] ^ writer.WzKey[i - 2]));
				compressedBytes = memStream.GetBuffer();
				writer.Close();
			}
		}
		#endregion
	}
}
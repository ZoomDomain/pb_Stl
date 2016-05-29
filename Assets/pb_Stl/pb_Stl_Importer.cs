﻿using UnityEngine;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Parabox.STL
{
	/**
	 * Import methods for STL files.
	 */
	public static class pb_Stl_Importer
	{
		const int MAX_FACETS_PER_MESH = 65535 / 3;

		class Facet
		{
			public Vector3 normal;
			public Vector3 a, b, c;

			public override string ToString()
			{
				return string.Format("{0:F2}: {1:F2}, {2:F2}, {3:F2}", normal, a, b, c);
			}
		}

		/**
		 * Import an STL file at path.
		 */
		public static Mesh[] Import(string path)
		{
			if( IsBinary(path) )
			{
				try
				{
					return ImportBinary(path);
				}
				catch(System.Exception e)
				{
					Debug.LogWarning(string.Format("Failed importing mesh at path {0}.\n{1}", path, e.ToString()));
					return null;
				}
			}
			else
			{
				return ImportAscii(path);
			}
		}

		private static Mesh[] ImportBinary(string path)
		{
			Facet[] facets;

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader br = new BinaryReader(fs, new ASCIIEncoding()))
                {
                    // read header
                    byte[] header = br.ReadBytes(80);
                    uint facetCount = br.ReadUInt32();
                    facets = new Facet[facetCount];

                    for(uint i = 0; i < facetCount; i++)
                    {
                    	facets[i] = new Facet();

                    	facets[i].normal.x = br.ReadSingle();
                    	facets[i].normal.y = br.ReadSingle();
                    	facets[i].normal.z = br.ReadSingle();

                    	facets[i].a.x = br.ReadSingle();
                    	facets[i].a.y = br.ReadSingle();
                    	facets[i].a.z = br.ReadSingle();

                    	facets[i].b.x = br.ReadSingle();
                    	facets[i].b.y = br.ReadSingle();
                    	facets[i].b.z = br.ReadSingle();

                    	facets[i].c.x = br.ReadSingle();
                    	facets[i].c.y = br.ReadSingle();
                    	facets[i].c.z = br.ReadSingle();

                    	// padding
                    	br.ReadUInt16();
                    }
                }
            }

			return CreateMeshWithFacets(facets);
		}

		const int SOLID = 1;
		const int FACET = 2;
		const int OUTER = 3;
		const int VERTEX = 4;
		const int ENDLOOP = 5;
		const int ENDFACET = 6;
		const int ENDSOLID = 7;
		const int EMPTY = 0;

		private static int ReadState(string line)
		{
			if(line.StartsWith("solid"))
				return SOLID;
			else if(line.StartsWith("facet"))
				return FACET;
			else if(line.StartsWith("outer"))
				return OUTER;
			else if(line.StartsWith("vertex"))
				return VERTEX;
			else if(line.StartsWith("endloop"))
				return ENDLOOP;
			else if(line.StartsWith("endfacet"))
				return ENDFACET;
			else if(line.StartsWith("endsolid"))
				return ENDSOLID;
			else
				return EMPTY;
		}

		private static Mesh[] ImportAscii(string path)
		{
			List<Facet> facets = new List<Facet>();

			using(StreamReader sr = new StreamReader(path))
			{
				string line;
				int state = EMPTY, vertex = 0;
				Facet f = null;
				bool exit = false;

				while(sr.Peek() > 0 && !exit)
				{
					line = sr.ReadLine().Trim();
					int previousState = state;
					state = ReadState(line);

					switch(state)
					{
						case SOLID:
							continue;
						break;

						case FACET:
							f = new Facet();
							f.normal = StringToVec3(line.Replace("facet normal ", ""));
						break;

						case OUTER:
							vertex = 0;
						break;

						case VERTEX:
							if(vertex == 0) f.a = StringToVec3(line.Replace("vertex ", ""));
							else if(vertex == 1) f.b = StringToVec3(line.Replace("vertex ", ""));
							else if(vertex == 2) f.c = StringToVec3(line.Replace("vertex ", ""));
							vertex++;
						break;

						case ENDLOOP:
						break;

						case ENDFACET:
							facets.Add(f);
						break;

						case ENDSOLID:
							exit = true;
						break;

						case EMPTY:
						default:
						break;

					}
				}
			}

			return CreateMeshWithFacets(facets);
		}

		private static Vector3 StringToVec3(string str)
		{
			string[] split = str.Trim().Split(null);
			Vector3 v = new Vector3();

			v.x = float.Parse(split[0], System.Globalization.CultureInfo.InvariantCulture);
			v.y = float.Parse(split[1], System.Globalization.CultureInfo.InvariantCulture);
			v.z = float.Parse(split[2], System.Globalization.CultureInfo.InvariantCulture);

			return v;
		}

		/**
		 * Read the first 80 bytes of a file and if they are all 0x0 it's likely
		 * that this file is binary.
		 */
		private static bool IsBinary(string path)
		{
			// http://stackoverflow.com/questions/968935/compare-binary-files-in-c-sharp
			FileInfo file = new FileInfo(path);

			if(file.Length < 130)
				return false;

			using(FileStream f0 = file.OpenRead())
			{
				using(BufferedStream bs0 = new BufferedStream(f0))
				{
					for(long i = 0; i < 80; i++)
					{
						if(bs0.ReadByte() != 0x0)
							return false;
					}
				}
			}

			return true;
		}

		/**
		 * @todo test with > USHORT_MAX vertex count meshes
		 */
		private static Mesh[] CreateMeshWithFacets(IList<Facet> facets)
		{
			int fl = facets.Count, f = 0, mvc = MAX_FACETS_PER_MESH * 3;
			Mesh[] meshes = new Mesh[fl / MAX_FACETS_PER_MESH + 1];

			for(int i = 0; i < meshes.Length; i++)
			{
				int len = System.Math.Min(mvc, (fl - f) * 3);
				Vector3[] v = new Vector3[len];
				Vector3[] n = new Vector3[len];
				int[] t = new int[len];

				for(int it = 0; it < len; it += 3)
				{
					v[it  ] = facets[f].a;
					v[it+1] = facets[f].b;
					v[it+2] = facets[f].c;

					n[it  ] = facets[f].normal;
					n[it+1] = facets[f].normal;
					n[it+2] = facets[f].normal;

					t[it  ] = it;
					t[it+1] = it+1;
					t[it+2] = it+2;

					f++;
				}

				meshes[i] = new Mesh();
				meshes[i].vertices = v;
				meshes[i].normals = n;
				meshes[i].triangles = t;
			}

			return meshes;
		}
	}
}

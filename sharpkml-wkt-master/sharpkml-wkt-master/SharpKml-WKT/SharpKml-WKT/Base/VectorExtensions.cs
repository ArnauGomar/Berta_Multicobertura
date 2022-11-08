using SharpKml.Base;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpKml_WKT.Base
{
	/// <summary>
	/// Provides extension methods for <see cref="Vector"/> objects.
	/// </summary>
	public static class VectorExtensions
	{
		public static string AsWKT(this Vector[][] polygon)
		{
			StringBuilder sb = new StringBuilder("((");
			sb.Append(polygon[0].AsCoordinateString());
			foreach (Vector[] innerRing in polygon.Skip(1))
			{
				sb.Append("),(");
				sb.Append(innerRing.AsCoordinateString());
			}
			sb.Append("))");
			return sb.ToString();
		}

		public static string AsCoordinateString(this Vector[] vectors)
		{
			List<string> cordStrings = vectors.Select(v => v.AsCoordinatePair()).ToList();
			return string.Join(", ", cordStrings);
		}

		public static string AsCoordinatePair(this Vector coordinate)
		{
			return $"{coordinate.Longitude} {coordinate.Latitude}";
		}
	}
}

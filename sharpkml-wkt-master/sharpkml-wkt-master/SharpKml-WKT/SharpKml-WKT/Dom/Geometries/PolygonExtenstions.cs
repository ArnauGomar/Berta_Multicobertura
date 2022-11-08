using SharpKml.Base;
using SharpKml.Dom;
using System.Collections.Generic;
using System.Linq;

namespace SharpKml_WKT.Dom.Geometries
{
	/// <summary>
	/// Provides extension methods for <see cref="Polygon"/> objects.
	/// </summary>
	public static class PolygonExtenstions
	{
		public static Vector[][] AsVectorCoordinates(this Polygon polygon)
		{
			List<List<Vector>> coordinates = new List<List<Vector>>();
			coordinates.Add(new List<Vector>());
			coordinates[0].AddRange(polygon.OuterBoundary.LinearRing.Coordinates);
			coordinates.AddRange(polygon.InnerBoundary.Select(inner => inner.LinearRing.Coordinates.ToList()));
			return coordinates.Select(c => c.ToArray()).ToArray();
		}
	}
}

using SharpKml.Base;
using SharpKml.Dom;
using SharpKml.Engine;
using SharpKml_WKT.Base;
using SharpKml_WKT.Dom.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpKml_WKT.Dom
{
	/// <summary>
	/// Provides extension methods for <see cref="Placemark"/> objects.
	/// </summary>
	public static class PlacemarkExtensions
	{
		/// <summary>
		/// Generates a WKT string for the polygons in a Placemark <see cref="Placemark"/>.
		/// Currently only supports placemarks with MultipleGeometry or Polygon Geometries.
		/// </summary>
		/// <param name="placemark">The placemark instance.</param>
		/// <returns>
		/// A <c>string</c> containing the well known text string for the geometry in the
		/// placemark.
		/// </returns>
		/// <exception cref="ArgumentNullException">placemark is null.</exception>
		/// <exception cref="ArgumentException">placemark geometry is not a MultipleGeometry or Polygon.</exception>
		public static string AsWKT(this Placemark placemark)
		{
			if (placemark == null)
			{
				throw new ArgumentNullException();
			}

			if (!(placemark.Geometry is MultipleGeometry) && !(placemark.Geometry is Polygon))
			{
				throw new NotImplementedException("Only implemented types are Polygon and MultiplePolygon");
			}

			List<Vector[][]> coordinates = placemark.ConvertToCoordinates();

			if (placemark.Geometry is MultipleGeometry)
			{
				return GenerateMultiplePolygonWKT(coordinates);
			}

			return GeneratePolygonWKT(coordinates.FirstOrDefault());

		}

		/// <summary>
		/// Generates a List of arrays of Vectors for each Polygon in the Placemark <see cref="Placemark"/>.
		/// </summary>
		/// <param name="placemark">The placemark instance.</param>
		/// <returns>
		/// A <c>ListVector[][]</c> containing the coordinates of each <see cref="Polygon"/> of the
		/// placemark.
		/// </returns>
		/// <exception cref="ArgumentNullException">placemark is null.</exception>
		/// <exception cref="ArgumentException">placemark geometry is not a MultipleGeometry or Polygon.</exception>
		private static List<Vector[][]> ConvertToCoordinates(this Placemark placemark)
		{
			if (placemark == null)
			{
				throw new ArgumentNullException();
			}

			if (!(placemark.Geometry is MultipleGeometry) && !(placemark.Geometry is Polygon))
			{
				throw new ArgumentException("Expecting MultipleGeometry or Polygon");
			}

			List<Vector[][]> Polygons = new List<Vector[][]>();

			foreach (Polygon polygon in placemark.Flatten().OfType<Polygon>())
			{
				Polygons.Add(polygon.AsVectorCoordinates());
			}

			return Polygons;
		}

		/// <summary>
		/// Generates a Multipolygon WKT string for a MultipleGeometry that has been 
		/// extracted from a <see cref="Placemark"/>.
		/// </summary>
		/// <param name="polygons">The list of polygon vectors.</param>
		/// <returns>
		/// A <c>string</c> containing the WKT data of every <see cref="Polygon"/> in the
		/// placemark.
		/// </returns>
		private static string GenerateMultiplePolygonWKT(List<Vector[][]> polygons)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("MULTIPOLYGON(");
			sb.Append(polygons[0].AsWKT());
			foreach (var polygon in polygons.Skip(1))
			{
				sb.Append(",");
				sb.Append(polygon.AsWKT());
			}
			sb.Append(")");
			return sb.ToString();

		}

		/// <summary>
		/// Generates a Polygon WKT string for a Polygon that has been 
		/// extracted from a <see cref="Placemark"/>.
		/// </summary>
		/// <param name="polygon">The polygon vectors.</param>
		/// <returns>
		/// A <c>string</c> containing the WKT data of a <see cref="Polygon"/> in the
		/// placemark.
		/// </returns>
		private static string GeneratePolygonWKT(Vector[][] polygon)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("POLYGON(");
			sb.Append(polygon.AsWKT());
			sb.Append(")");
			return sb.ToString();
		}
	}
}

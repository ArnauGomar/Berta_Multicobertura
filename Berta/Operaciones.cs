using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Precision;
using SharpKml.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Berta
{
    using Point = Tuple<double, double>; //Necesario para algoritmo RamerDouglasPeucker
    /// <summary>
    /// Conjunto de herramientas utilizadas o listas para utilizar
    /// </summary>
    public static class Operaciones
    {
        public static int CrearKML_KMZ(SharpKml.Dom.Document Doc, string NombreDoc, string carpeta, string Destino)
        {
            string path = Path.Combine(Path.Combine(@".\" + carpeta + "", NombreDoc + ".kml"));
            string path_destino = Path.Combine(Path.Combine(Destino, NombreDoc + ".kmz"));

            try
            {
                //Guardar Documento dentro del KML y exportar
                var kml = new SharpKml.Dom.Kml();
                kml.Feature = Doc; //DOCUMENTO
                                   //kml.Feature = placemark; //Se puede guardar directamente un placemark
                KmlFile kmlfile = KmlFile.Create(kml, true);

                //Eliminar archivo si existe (NO TIENE QUE EXISTIR, CATCH INTERNO)
                if (File.Exists(path))
                {
                    // If file found, delete it    
                    File.Delete(path);
                }

                using (var stream = File.OpenWrite(path)) //Path de salida
                {
                    kmlfile.Save(stream);
                }


                //Eliminar archivo en destino 
                if (File.Exists(path_destino))
                {
                    // If file found, delete it    
                    File.Delete(path_destino);
                }

                //Crear KMZ
                //Crear el archivo (si quieres puedes editar uno existente cambiando el modo a Update.
                using (ZipArchive archive = System.IO.Compression.ZipFile.Open(path_destino, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(path, Path.GetFileName(path));
                }

                //Eliminar archivo temporal
                if (File.Exists(path))
                {
                    // If file found, delete it    
                    File.Delete(path);
                }

                return 0;
            }
            catch
            {
                //Eliminar archivo temporal
                if (File.Exists(path))
                {
                    // If file found, delete it    
                    File.Delete(path);
                }

                //Eliminar archivo en destino 
                if (File.Exists(path_destino))
                {
                    // If file found, delete it    
                    File.Delete(path_destino);
                }

                return -1;
            }


        } //creamos un KML nuevo y se guarda en carpeta asignada (temporales). Después se crea un KMZ y se guarda en la carpeta asignada.

        private static double PerpendicularDistance(Point pt, Point lineStart, Point lineEnd)
        {
            double dx = lineEnd.Item1 - lineStart.Item1;
            double dy = lineEnd.Item2 - lineStart.Item2;

            // Normalize
            double mag = Math.Sqrt(dx * dx + dy * dy);
            if (mag > 0.0)
            {
                dx /= mag;
                dy /= mag;
            }
            double pvx = pt.Item1 - lineStart.Item1;
            double pvy = pt.Item2 - lineStart.Item2;

            // Get dot product (project pv onto normalized direction)
            double pvdot = dx * pvx + dy * pvy;

            // Scale line direction vector and subtract it from pv
            double ax = pvx - pvdot * dx;
            double ay = pvy - pvdot * dy;

            return Math.Sqrt(ax * ax + ay * ay);
        } //Método 1 de Ramer-Douglas-Peucker line simplification

        private static void RamerDouglasPeucker(List<Point> pointList, double epsilon, List<Point> output)
        {
            if (pointList.Count < 2)
            {
                throw new ArgumentOutOfRangeException("Not enough points to simplify");
            }

            // Find the point with the maximum distance from line between the start and end
            double dmax = 0.0;
            int index = 0;
            int end = pointList.Count - 1;
            for (int i = 1; i < end; ++i)
            {
                double d = PerpendicularDistance(pointList[i], pointList[0], pointList[end]);
                if (d > dmax)
                {
                    index = i;
                    dmax = d;
                }
            }

            // If max distance is greater than epsilon, recursively simplify
            if (dmax > epsilon)
            {
                List<Point> recResults1 = new List<Point>();
                List<Point> recResults2 = new List<Point>();
                List<Point> firstLine = pointList.Take(index + 1).ToList();
                List<Point> lastLine = pointList.Skip(index).ToList();
                RamerDouglasPeucker(firstLine, epsilon, recResults1);
                RamerDouglasPeucker(lastLine, epsilon, recResults2);

                // build the result list
                output.AddRange(recResults1.Take(recResults1.Count - 1));
                output.AddRange(recResults2);
                if (output.Count < 2) throw new Exception("Problem assembling output");
            }
            else
            {
                // Just return start and end points
                output.Clear();
                output.Add(pointList[0]);
                output.Add(pointList[pointList.Count - 1]);
            }
        } //Método 2 de Ramer-Douglas-Peucker line simplification

        public static Geometry AplicarRamerDouglasPeucker(Coordinate[] C, double Epsilon)
        {

            //Solo si el area es superior a 0.1 consideramos válido el poligono. 
            //Eliminamos líneas erroneas dentro del propio poligono. 

            //Transformamos puntos NetTopology en Puntos RamerDouglas
            List<Coordinate> CoordenadasIn = C.ToList();
            List<Point> PuntosIn = new List<Point>();

            foreach (Coordinate coordenada in CoordenadasIn)
            {
                PuntosIn.Add(new Point(coordenada.X, coordenada.Y));
            }

            //Aplicar algoritmo RamerDouglasPeucker
            List<Point> PuntosOut = new List<Point>();
            RamerDouglasPeucker(PuntosIn, Epsilon, PuntosOut);

            //Transformamos puntos en coordenadas NetTopology
            List<Coordinate> CoordenadasOut = new List<Coordinate>();
            foreach (Point punto in PuntosOut)
            {
                CoordenadasOut.Add(new Coordinate(punto.Item1, punto.Item2));
            }

            //Creamos nuevo poligono y lo añadimos a la lista
            var gff = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(); //generador de poligonos
            Polygon polyOut = gff.CreatePolygon(CoordenadasOut.ToArray());

            return polyOut;
        } //Aplica el algoritmo RamerDouglasPeucker

        public static Geometry ReducirPrecision(Geometry geom)
        {
            var pm = new PrecisionModel(10000); //10000

            var reducedGeom = GeometryPrecisionReducer.Reduce(geom, pm);

            return reducedGeom;
        } //Reducción de precisión de las coordenadas de los poligonos
    }
}

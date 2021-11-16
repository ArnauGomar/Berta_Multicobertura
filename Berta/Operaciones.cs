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

        public static void GuardarAjustes(int CvsM, double epsilon, double epsilon_simple)
        {
            StreamWriter W = new StreamWriter("Ajustes.txt");
            W.WriteLine(CvsM);
            W.WriteLine(epsilon);
            W.WriteLine(epsilon_simple);
            W.Close();
        } //Guarda los datos del CvsM, epsilon (multiple) y epsilon_simple en el txt

        public static (List<Cobertura>,List<string>) CargarCoberturas (DirectoryInfo DI, string FL_IN)
        {
            List<Cobertura> Originales = new List<Cobertura>(); //Lista a retornar, coberturas
            List<string> NombresCargados = new List<string>(); //Lista a retornar, nombres

            foreach (var file in DI.GetFiles())
            {

                //Abrir KML
                (FileStream H, string Nombre) =  AbrirKMLdeKMZ(file.FullName);

                if (FL_IN == null)
                {
                    FL_IN = Nombre.Split('-')[1];
                }
                else if (Nombre.Split('-').Count()>1)
                {
                    FL_IN = Nombre.Split('-')[1];
                }

                //if (File.Exists(Path.Combine(@".\Temporal", file.Name.Split(".")[0] + ".kml")))
                //{
                //    H = File.Open(Path.Combine(@".\Temporal", file.Name.Split(".")[0] + ".kml"), FileMode.Open); //Abrir KML  
                //    FileName = file.Name.Split(".")[0];
                //    Nombre = FileName;
                //    if (FL_IN == null)
                //    {
                //        FL_IN = Nombre.Split('-')[1];
                //    }
                //}

                //else
                //{
                //    H = File.Open(Path.Combine(@".\Temporal", "doc.kml"), FileMode.Open); //Abrir KML generico 
                //    FileName = "doc";
                //    Nombre = file.Name.Split(".")[0];
                //    if (FL_IN == null)
                //    {
                //        FL_IN = Nombre.Split('-')[1];
                //    }
                //}

                
                ////Eliminar archivo temporal
                //if (File.Exists(Path.Combine(@".\Temporal", "" + FileName + ".kml")))
                //{
                //    // If file found, delete it    
                //    File.Delete(Path.Combine(@".\Temporal", "" + FileName + ".kml"));
                //}

                //KmlFile F = KmlFile.Load(H); //Cargar KML
                //H.Close();

                //var polyGON = F.Root.Flatten().OfType<SharpKml.Dom.Polygon>().ToList(); //Extraer lista de poligonos del KML

                //List<Geometry> Poligonos = new List<Geometry>(); //Lista donde se guardaran los poligonos

                ////Implementación múltiples poligonos
                //foreach (SharpKml.Dom.Polygon poly in polyGON)
                //{
                //    SharpKml.Dom.CoordinateCollection Coordenadas = poly.OuterBoundary.LinearRing.Coordinates; //Extraer coordenadas del poligono SharpKml (solo coordenadas externas no huecos)

                //    List<SharpKml.Base.Vector> A = new List<SharpKml.Base.Vector>(); //Guardar coordenadas del poligono en una lista generica (paso necesario para poder extraer lat y long)
                //    foreach (var c in Coordenadas)
                //    {
                //        A.Add(c);
                //    }
                //    //Guardar coordenadas del poligono en un vector de coordenadas NetTopologySuite
                //    int max = Coordenadas.Count();
                //    Coordinate[] Coordenades = new Coordinate[max];
                //    int i = 0;
                //    while (i < max)
                //    {
                //        Coordenades[i] = new Coordinate(A[i].Longitude, A[i].Latitude);
                //        i++;
                //    }
                //    //Crear poligono NetTopologySuite
                //    var gf = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();
                //    Geometry poly_T = gf.CreatePolygon(Coordenades); //Poligono a computar!

                //    //Implementación huecos
                //    List<Geometry> Huecos = new List<Geometry>(); //Guardar huecos existentes

                //    if (poly.InnerBoundary != null)
                //    {
                //        foreach (SharpKml.Dom.InnerBoundary IB in poly.InnerBoundary)
                //        {
                //            SharpKml.Dom.CoordinateCollection Coordenadas_Hueco = IB.LinearRing.Coordinates;
                //            List<SharpKml.Base.Vector> B = new List<SharpKml.Base.Vector>();

                //            //Guardar coordenadas del poligono en una lista generica (paso necesario para poder extraer lat y long)
                //            foreach (var c in Coordenadas_Hueco)
                //            {
                //                B.Add(c);
                //            }
                //            //Guardar coordenadas del poligono en un vector de coordenadas NetTopologySuite
                //            int maxx = Coordenadas_Hueco.Count();
                //            Coordinate[] Coordenadess = new Coordinate[maxx];
                //            int ii = 0;
                //            while (ii < maxx)
                //            {
                //                Coordenadess[ii] = new Coordinate(B[ii].Longitude, B[ii].Latitude);
                //                ii++;
                //            }
                //            //Crear poligono NetTopologySuite
                //            var gff = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();
                //            Geometry poly_T_H = gff.CreatePolygon(Coordenadess); //Poligono a computar!
                //            poly_T = poly_T.Difference(poly_T_H);
                //        }
                //    }

                //    Poligonos.Add(poly_T); //Añadir poligono a la lista para generar cobertura
                //}

                List<Geometry> Poligonos = TraducirPoligono(H, Nombre); //Carga kml, extrae en SharpKML y traduce a NTS

                Originales.Add(new Cobertura(Nombre.Split('-')[0], FL_IN, "original", Poligonos));

                Console.WriteLine(Nombre);
                NombresCargados.Add(Nombre);
            }

            return (Originales, NombresCargados);
        } //Carga las coberturas del fichero KML/KMZ

        public static (FileStream,string) AbrirKMLdeKMZ (string path)
        {
            //Eliminar todo dentro de carpeta temporal
            DirectoryInfo TemporalC = new DirectoryInfo(@"Temporal");
            foreach (System.IO.FileInfo file in TemporalC.GetFiles()) file.Delete();
            foreach (System.IO.DirectoryInfo subDirectory in TemporalC.GetDirectories()) subDirectory.Delete(true);

            string[] MirarSiKMZ = path.Split(".");
            string Nombre = Path.GetFileNameWithoutExtension(path);
            FileStream H = null;
            if (MirarSiKMZ[1] == "kmz") //Abrir en formato kmz
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(path, @".\Temporal"); //extraer KML SACTA
                if (File.Exists(Path.Combine(@".\Temporal", Nombre + ".kml")))
                {
                    H = File.Open(Path.Combine(@".\Temporal", Nombre + ".kml"), FileMode.Open); //Abrir KML  
                }
                else
                {
                    H = File.Open(Path.Combine(@".\Temporal", "doc.kml"), FileMode.Open); //Abrir KML generico 
                }
            }
            else //Abrir en formato kml
            {
                if (File.Exists(path))
                {
                    H = File.Open(path, FileMode.Open); //Abrir KML  
                }
            }

            return (H,Nombre);
        }

        public static List<Geometry> TraducirPoligono (FileStream H, string FileName)
        {
            KmlFile F = KmlFile.Load(H); //Cargar KML
            H.Close();

            //Eliminar archivo temporal
            if (File.Exists(Path.Combine(@".\Temporal", "" + FileName + ".kml")))
            {
                // If file found, delete it    
                File.Delete(Path.Combine(@".\Temporal", "" + FileName + ".kml"));
            }

            var polyGON = F.Root.Flatten().OfType<SharpKml.Dom.Polygon>().ToList(); //Extraer lista de poligonos del KML

            List<Geometry> Poligonos = new List<Geometry>(); //Lista donde se guardaran los poligonos

            //Implementación múltiples poligonos
            foreach (SharpKml.Dom.Polygon poly in polyGON)
            {
                SharpKml.Dom.CoordinateCollection Coordenadas = poly.OuterBoundary.LinearRing.Coordinates; //Extraer coordenadas del poligono SharpKml (solo coordenadas externas no huecos)

                List<SharpKml.Base.Vector> A = new List<SharpKml.Base.Vector>(); //Guardar coordenadas del poligono en una lista generica (paso necesario para poder extraer lat y long)
                foreach (var c in Coordenadas)
                {
                    A.Add(c);
                }
                //Guardar coordenadas del poligono en un vector de coordenadas NetTopologySuite
                int max = Coordenadas.Count();
                Coordinate[] Coordenades = new Coordinate[max];
                int i = 0;
                while (i < max)
                {
                    Coordenades[i] = new Coordinate(A[i].Longitude, A[i].Latitude);
                    i++;
                }
                //Crear poligono NetTopologySuite
                var gf = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();
                Geometry poly_T = gf.CreatePolygon(Coordenades); //Poligono a computar!

                //Implementación huecos
                List<Geometry> Huecos = new List<Geometry>(); //Guardar huecos existentes

                if (poly.InnerBoundary != null)
                {
                    foreach (SharpKml.Dom.InnerBoundary IB in poly.InnerBoundary)
                    {
                        SharpKml.Dom.CoordinateCollection Coordenadas_Hueco = IB.LinearRing.Coordinates;
                        List<SharpKml.Base.Vector> B = new List<SharpKml.Base.Vector>();

                        //Guardar coordenadas del poligono en una lista generica (paso necesario para poder extraer lat y long)
                        foreach (var c in Coordenadas_Hueco)
                        {
                            B.Add(c);
                        }
                        //Guardar coordenadas del poligono en un vector de coordenadas NetTopologySuite
                        int maxx = Coordenadas_Hueco.Count();
                        Coordinate[] Coordenadess = new Coordinate[maxx];
                        int ii = 0;
                        while (ii < maxx)
                        {
                            Coordenadess[ii] = new Coordinate(B[ii].Longitude, B[ii].Latitude);
                            ii++;
                        }
                        //Crear poligono NetTopologySuite
                        var gff = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();
                        Geometry poly_T_H = gff.CreatePolygon(Coordenadess); //Poligono a computar!
                        poly_T = poly_T.Difference(poly_T_H);
                    }
                }

                Poligonos.Add(poly_T); //Añadir poligono a la lista para generar cobertura
            }

            return Poligonos;
        }
    }
}

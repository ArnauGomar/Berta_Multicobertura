using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Precision;
using SharpKml.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
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
        /// <summary>
        /// Metodo para exportar un KMZ con los cálculos.
        /// </summary>
        /// <param name="Doc"></param>
        /// <param name="NombreDoc"></param>
        /// <param name="carpeta"></param>
        /// <param name="Destino"></param>
        /// <returns></returns>
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


        }

        public static int CrearKML_KMZ(List<SharpKml.Dom.Folder> Carpetas, string NombreDoc, string carpeta, string Destino)
        {
            string path = Path.Combine(Path.Combine(@".\" + carpeta + "", NombreDoc + ".kml"));
            string path_destino = Path.Combine(Path.Combine(Destino, NombreDoc + ".kmz"));

            try
            {
                //Guardar Documento dentro del KML y exportar
                var kml = new SharpKml.Dom.Kml();
                SharpKml.Dom.Document DOC = new SharpKml.Dom.Document();
                foreach(SharpKml.Dom.Folder Carpeta in Carpetas)
                {
                    DOC.AddFeature(Carpeta);
                }
                kml.Feature = DOC; //DOCUMENTO
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


        }

        /// <summary>
        /// Método 1 de Ramer-Douglas-Peucker line simplification
        /// </summary>
        /// <param name="pt"></param>
        /// <param name="lineStart"></param>
        /// <param name="lineEnd"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Método 2 de Ramer-Douglas-Peucker line simplification
        /// </summary>
        /// <param name="pointList"></param>
        /// <param name="epsilon"></param>
        /// <param name="output"></param>
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

        /// <summary>
        /// //Aplica el algoritmo RamerDouglasPeucker
        /// </summary>
        /// <param name="C"></param>
        /// <param name="Epsilon"></param>
        /// <returns></returns>
        public static Geometry AplicarRamerDouglasPeucker(Geometry In, double Epsilon)
        {
            //Solo si el area es superior a 0.1 consideramos válido el poligono. 
            //Eliminamos líneas erroneas dentro del propio poligono. 

            //Transformamos puntos NetTopology en Puntos RamerDouglas
            List<Coordinate> CoordenadasIn = In.Coordinates.ToList();
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

        /// <summary>
        /// Reduce la precisión (número de decimales de las coordenadas) de una geometria en concreto.
        /// </summary>
        /// <param name="geom"></param>
        /// <returns></returns>
        public static Geometry ReducirPrecision(Geometry geom)
        {
            var pm = new PrecisionModel(10000); //10000

            var reducedGeom = GeometryPrecisionReducer.Reduce(geom, pm);

            return reducedGeom;
        } //Reducción de precisión de las coordenadas de los poligonos

        public static Geometry ReducirPrecision(Geometry geom, int Cord)
        {
            var pm = new PrecisionModel(Cord); //10000

            var reducedGeom = GeometryPrecisionReducer.Reduce(geom, pm);

            return reducedGeom;
        } //Reducción de precisión de las coordenadas de los poligonos

        /// <summary>
        /// Guarda los datos del CvsM, epsilon y epsilon_simple en un txt
        /// </summary>
        /// <param name="CvsM"></param>
        /// <param name="epsilon"></param>
        /// <param name="epsilon_simple"></param>
        public static void GuardarAjustes(int CvsM, double epsilon, double epsilon_simple)
        {
            StreamWriter W = new StreamWriter("Ajustes.txt");
            W.WriteLine(CvsM);
            W.WriteLine(epsilon);
            W.WriteLine(epsilon_simple);
            W.Close();
        } //Guarda los datos del CvsM, epsilon (multiple) y epsilon_simple en el txt

        /// <summary>
        /// Lee un fichero KML/KMZ para extraer las coberturas (geometrias) que contiene
        /// </summary>
        /// <param name="DI"></param>
        /// <param name="FL_IN"></param>
        /// <returns></returns>
        public static (List<Cobertura>,List<string>) CargarCoberturas (DirectoryInfo DI, string FL_IN, string [] args, int CvsM)
        {
            List<Cobertura> Originales = new List<Cobertura>(); //Lista a retornar, coberturas
            List<string> NombresCargados = new List<string>(); //Lista a retornar, nombres
            bool Empty = false; //Controlar el error de traducción de un poligono.
            string FL_Obtenido = null;

            foreach (var file in DI.GetFiles())
            {

                //Abrir KML
                (FileStream H, string Nombre) =  AbrirKMLdeKMZ(file.FullName);

                //Problemática - FLXXX o _FLXXX +problemática FL en filtrado SACTA
                string Nombre_sin_fl = "ERROR";
                string[] V = Nombre.Split('-');
                if (V.Length > 1) //Nombre en formato XX_XXXXXXX-FLXXX
                {
                    if (FL_IN == null)
                    {
                        FL_Obtenido = V[1];
                    }
                        
                    Nombre_sin_fl = V[0];
                }
                else
                {
                    V = Nombre.Split('_'); //Nombre en formato XX_XXXXXXX_FLXXX
                    if (V.Length > 1)
                    {
                        if (FL_IN == null)
                        {
                            FL_Obtenido = V.Last();
                        }
                            
                        List<string> L = V.ToList();
                        L.Remove(V.Last());
                        Nombre_sin_fl = string.Join('_', L);
                    }
                }

                List<Geometry> Poligonos = TraducirPoligono(H, Nombre_sin_fl, args, CvsM, Nombre); //Carga kml, extrae en SharpKML y traduce a NTS
                 
                int k = 0; 
                while((k<Poligonos.Count)&&(!Empty))
                {
                    if (Poligonos[k].IsEmpty == true)
                        Empty = true;
                    k++;
                }

                if(!Empty) //No hay error
                {
                    if (FL_IN != null)
                        Originales.Add(new Cobertura(Nombre_sin_fl, FL_IN, "original", Poligonos));
                    else
                        Originales.Add(new Cobertura(Nombre_sin_fl, FL_Obtenido, "original", Poligonos));

                    Console.WriteLine(Nombre);
                    NombresCargados.Add(Nombre);
                }
                else //Hay error
                {
                    Console.WriteLine(Nombre+ " NO CARGADO");
                    break;
                }

            }

            if (!Empty)
                return (Originales, NombresCargados);
            else
                return (null, null);

        } //Carga las coberturas del fichero KML/KMZ

        /// <summary>
        /// Descomprime un archivo KMZ 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
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
                    try
                    {
                        H = File.Open(Path.Combine(@".\Temporal", "doc.kml"), FileMode.Open); //Abrir KML generico 
                    }
                    catch //Posiblemente se ha cambiado el nombre del archivo a mano
                    {
                        string[] V = new string[2];
                        string aBuscar = "";

                        V = Nombre.Split('-');
                        aBuscar = V[0];
                        if(V.Count()==1)
                        {
                            V = Nombre.Split('_');
                            aBuscar = V[1];
                        }

                        FileInfo[] TodosEnTemporal = TemporalC.GetFiles();
                        FileInfo Buscado = TodosEnTemporal.Where(x => x.Name.Contains(aBuscar) == true).ToList()[0];
                        H = File.Open(Buscado.FullName, FileMode.Open); 
                    }
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

        /// <summary>
        /// Traduce un objeto polygon de la libreria SharpKML a un objeto de la libreria NetTopologySuite
        /// </summary>
        /// <param name="H"></param>
        /// <param name="FileName"></param>
        /// <returns></returns>
        public static List<Geometry> TraducirPoligono (FileStream H, string FileName, string [] args, int CvsM, string NombreCompleto)
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
            var MpolyGON = F.Root.Flatten().OfType<SharpKml.Dom.MultipleGeometry>().ToList();

            SharpKml.Dom.Document D = new SharpKml.Dom.Document();

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
                Geometry poly_T = gf.CreateEmpty(Dimension.Curve);

                try
                {
                    poly_T = gf.CreatePolygon(Coordenades); //Poligono a computar!
                }
                catch (Exception e)
                {
                    Console.WriteLine(NombreCompleto + " ERROR DE LECTURA POLIGONAL (" + e.Message+")");
                    if (CvsM == 1)
                        Operaciones.EscribirOutput(args, NombreCompleto + " ERROR DE LECTURA POLIGONAL (" + e.Message + ") NO SE HA EJECUTADO EL CÁLCULO");
                    else
                    {
                        Console.ReadLine();
                    }
                        

                }

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
                        if(poly_T.Intersects(poly_T_H)==true) //Intersecta, por lo que esta dentro de los limites, es un hueco
                            poly_T = poly_T.Difference(poly_T_H);
                        else //No intersecta, por lo que es una parte exterior (otro poligono) del conjunto
                            poly_T = poly_T.Union(poly_T_H);
                        
                    }
                }

                Poligonos.Add(poly_T); //Añadir poligono a la lista para generar cobertura

            }

            return Poligonos;
        }


        public static Geometry RepararPoligono (Geometry Corrupta, GeometryFactory gff)
        {
            List<Geometry> Geometrias_extraidas = new List<Geometry>();
            int i = 0; 
            while(i< Convert.ToInt32(Corrupta.NumGeometries))
            {
                Geometrias_extraidas.Add(Corrupta.GetGeometryN(i));
                i++;
            }
            CascadedPolygonUnion ExecutarUnion = new CascadedPolygonUnion(Geometrias_extraidas);
            Geometry Area_NovTot_MP = ExecutarUnion.Union();
            //List<Geometry> Geometrias_extraidas = (GeometryCollection)Corrupta.Geom
            //return Reparada;
            return Area_NovTot_MP;
        }

        /// <summary>
        /// Reordena los resultados para mostrar por radar y no por nivel.
        /// </summary>
        /// <param name="conjunto"></param>
        /// <param name="CoberturasSimples"></param>
        /// <param name="Listado_ConjuntoCoberturasMultiples"></param>
        /// <param name="CoberturaMaxima"></param>
        /// <returns></returns>
        public static SharpKml.Dom.Folder CarpetaRedundados(Conjunto conjunto, Conjunto CoberturasSimples, List<Conjunto> Listado_ConjuntoCoberturasMultiples, Cobertura CoberturaMaxima)
        {
            SharpKml.Dom.Folder Redundados = new SharpKml.Dom.Folder { Id = "Redundantes", Name = "Multi-cobertura por radar", Visibility = false }; //Carpeta donde se guardaran los resultados radar a radar
            List<SharpKml.Dom.Folder> PorRadar = new List<SharpKml.Dom.Folder>(); //Carpeta de cada radar

            //Cobertura base

            foreach (Cobertura COB in conjunto.A_Operar)
            {
                //Crear una carpeta para cada radar y añadir la cobertura base
                PorRadar.Add(new SharpKml.Dom.Folder { Id = COB.nombre, Name = COB.nombre + " " + COB.FL, Visibility = false });
                PorRadar.Last().AddFeature(COB.CrearDocumentoSharpKML());
            }
            

            //Creamos carpetas por lvl de cada radar
            List<SharpKml.Dom.Folder> PorNivelPorRadar = new List<SharpKml.Dom.Folder>();
            int k = 1; //Desde lvl 1 (simple) a lvl máx posible
            while (k <= PorRadar.Count())
            {
                foreach (Cobertura COB in conjunto.A_Operar)
                {
                    PorNivelPorRadar.Add(new SharpKml.Dom.Folder { Id = COB.nombre + "-" + k, Name = "Multi-cobertura " + string.Format("{0:00}", k) + " " + COB.FL, Visibility = false });
                }
                k++;
            }

            //Cobertura simple
            foreach (Cobertura COB in CoberturasSimples.A_Operar)
            {
                //En las carpetas PorNivelPorRadar de grado 1 (simple) añadimos la cobertura en qüestión
                PorNivelPorRadar[PorNivelPorRadar.IndexOf(PorNivelPorRadar.Where(x => x.Id == COB.nombre + "-1").ToList()[0])].AddFeature(COB.CrearDocumentoSharpKML());
            }


            //Cobertura multiple
            foreach (Conjunto con in Listado_ConjuntoCoberturasMultiples)
            {
                //Añadir multicoberturas
                foreach (Cobertura cob in con.A_Operar)
                {
                    //Buscamos los radares participantes y guardamos en la carpeta de nivel correspondiente
                    string[] RadParticipantes = cob.nombre.Split('.');
                    int nivel_M = cob.tipo_multiple;
                    foreach (string Radar in RadParticipantes)
                    {
                        PorNivelPorRadar[PorNivelPorRadar.IndexOf(PorNivelPorRadar.Where(x => x.Id == Radar + "-" + nivel_M).ToList()[0])].AddFeature(cob.CrearDocumentoSharpKML());
                    }
                }
            }

            //Añadimos cobertura máxima si es que existe (crear carpeta)
            if (CoberturaMaxima != null)
            {
                //Buscamos carpetas pertinentes de cada radar y añadimos la cobertura máxima
                foreach (Cobertura COB in conjunto.A_Operar)
                {
                    PorNivelPorRadar[PorNivelPorRadar.IndexOf(PorNivelPorRadar.Where(x => x.Id == COB.nombre + "-" + conjunto.A_Operar.Count()).ToList()[0])].AddFeature(CoberturaMaxima.CrearDocumentoSharpKML());
                }
            }

            //Ordenamos en cada carpeta PorRadar las carpetas PorNivelPorRadar
            foreach (var carpeta in PorNivelPorRadar)
            {
                if (carpeta.Features.Count != 0) //Si esta llena de información añadimos a carpeta correspondiente
                    PorRadar[PorRadar.IndexOf(PorRadar.Where(x => x.Id == carpeta.Id.Split('-')[0]).ToList()[0])].AddFeature(carpeta);
            }
            //Se añaden en carpeta redundados
            foreach (var carpeta in PorRadar)
            {
                Redundados.AddFeature(carpeta);
            }

            return Redundados;
        }

        public static SharpKml.Dom.Folder CarpetaRedundados_CM(List<string> Nombres, List<Conjunto> AnillosFL, int FL_ini, int FL_fin, int FL_itera)
        {
            List<SharpKml.Dom.Folder> Carpetas_Radar = new List<SharpKml.Dom.Folder>(); //Carpeta de cada radar
            List<string> Nombres_sin_fl = new List<string>();
            SharpKml.Dom.Folder Carpeta_return = new SharpKml.Dom.Folder();
            try
            {
                //Nombres.ForEach(x => Nombres_sin_fl.Add(string.Join("_", new string[2] { x.Split("_")[0], x.Split("_")[1] })));
                foreach (string N in Nombres)
                {
                    List<string> Trozos = N.Split("_").ToList();
                    Trozos.Remove(Trozos.Last());
                    Nombres_sin_fl.Add(string.Join("_",Trozos));
                }


            }
            catch
            {
                Nombres.ForEach(x => Nombres_sin_fl.Add(x.Split("-")[1]));
            }

            List<string> Nombres_finales = Nombres_sin_fl.Distinct().ToList(); //Quitamos los repetidos
            Nombres_finales.ForEach(x => Carpetas_Radar.Add(new SharpKml.Dom.Folder { Id = x, Name = x, Visibility = false })); //Creamos carpetas de cada radar

            //Creamos carpetas de cada radar, seleccionamos último FL ya que debería contener todos los radares (no tiene porque)
            //foreach (Cobertura cob in AnillosFL.Last().A_Operar)
            //{
            //    if (cob.tipo_multiple == 0) //Para coberturas simples ejecutamos la creación de carpeta normal
            //    {
            //        if(Carpetas_Radar.Where(x => x.Name == cob.nombre).ToList().Count == 0) //Si no existe ninguna carpeta ya de ese radar
            //                {
            //            Carpetas_Radar.Add(new SharpKml.Dom.Folder { Id = cob.nombre, Name = cob.nombre, Visibility = false });
            //        }
            //    }
            //    else
            //    {
            //        if (Carpetas_Radar.Count() > 0) //Si existen elementos
            //        {
            //            string[] Nombres = cob.nombre.Split(".");
            //            foreach (string Nombre in Nombres)
            //            {
            //                if (Carpetas_Radar.Where(x => x.Name == Nombre).ToList().Count == 0) //Si no existe ninguna carpeta ya de ese radar
            //                {
            //                    Carpetas_Radar.Add(new SharpKml.Dom.Folder { Id = Nombre, Name = Nombre, Visibility = false });
            //                }
            //            }
            //        }
            //        else
            //        {
            //            string[] Nombres = cob.nombre.Split(".");
            //            foreach (string Nombre in Nombres)
            //            {
            //                Carpetas_Radar.Add(new SharpKml.Dom.Folder { Id = Nombre, Name = Nombre, Visibility = false });
            //            }
            //        }
            //    }
            //}

            //Ahora, para cada radar buscaremos todas las coberturas implicadas de los distintos FL
            foreach (SharpKml.Dom.Folder Radar in Carpetas_Radar)
            {
                List<SharpKml.Dom.Folder> FLs = new List<SharpKml.Dom.Folder>();
                while(FL_ini<=FL_fin) //Mientras no lleguemos al último FL.
                {
                    string FLXXX = "";
                    if (FL_ini < 10)
                        FLXXX = "FL00" + FL_ini;
                    else if (FL_ini < 100)
                        FLXXX = "FL0" + FL_ini;
                    else
                        FLXXX = "FL" + FL_ini;

                    //Seleccionamos todas las coberturas de ese FL que corresponden al radar buscado
                    List<Cobertura> Coberturas_FL = AnillosFL.Where(x => x.FL == FLXXX).ToList()[0].A_Operar.Where(y => ((y.nombre == Radar.Name) || (y.nombre.Split(".").Contains(Radar.Name) == true))).ToList();
                    List<Cobertura> Coberturas_FL2 = new List<Cobertura>();
                    FLs.Add(new SharpKml.Dom.Folder { Id = FLXXX, Name = FLXXX, Visibility = false }); //Generamos carpetas para FL y guardamos info en ella
                    foreach(Cobertura cob in Coberturas_FL)
                    {
                        FLs.Last().AddFeature(cob.CrearDocumentoSharpKML());
                    }

                    FL_ini = FL_ini + FL_itera;
                }

                //Añadimos carpetas a la carpeta del radar.
                foreach (SharpKml.Dom.Folder FL in FLs)
                    Radar.AddFeature(FL);

                Carpeta_return.AddFeature(Radar);
            }

            //Para cada radar, creamos



            //SharpKml.Dom.Folder Redundados = new SharpKml.Dom.Folder { Id = "Redundantes", Name = "Cobertura mínima por radar", Visibility = false }; //Carpeta donde se guardaran los resultados radar a radar
            //List<SharpKml.Dom.Folder> PorRadar = new List<SharpKml.Dom.Folder>(); //Carpeta de cada radar

            //foreach (Conjunto anillo in AnillosFL)
            //{
            //    foreach (Cobertura cob in anillo.A_Operar)
            //    {
            //        if(PorRadar.Count==0) //Si no existe ninguna carpeta
            //        {
            //            if(cob.tipo_multiple==0) //Cobertura simple
            //            {
            //                PorRadar.Add(new SharpKml.Dom.Folder { Id = cob.nombre, Name = cob.nombre + " " + cob.FL, Visibility = false });
            //                PorRadar.Last()
            //            }
            //        }
            //    }
            //}


            return Carpeta_return; 
        }

        /// <summary>
        /// Parte del menú del cálculo de multi-cobertura que pregunta por el FL
        /// </summary>
        /// <returns></returns>
        public static string Menu_FL() //Preguntar FL
        {
            bool FL_correcto = false;
            string FL_IN = null;
            while (!FL_correcto)
            {
                Console.Clear();
                Console.WriteLine("Berta T");
                Console.WriteLine();
                Console.WriteLine("1 - Cálculo de multi-coberturas");
                Console.WriteLine();
                Console.WriteLine("FL seleccionado (p.e.: FL100 / FL090):");

                //Obtener el FL
                FL_IN = Console.ReadLine();
                List<char> chars = new List<char>();
                foreach (char c in FL_IN)
                {
                    chars.Add(c);
                }
                
                try
                {
                    long N = 0; //Comprobar que el FL es correcto, solo si lo es el programa seguira
                    if ((chars[0] == 'F') && (chars[1] == 'L') && (long.TryParse(chars[2].ToString(), out N)) && (long.TryParse(chars[3].ToString(), out N)) && (long.TryParse(chars[4].ToString(), out N)))
                        FL_correcto = true;
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine("El FL indicado no es correcto");
                        Console.ReadLine();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                    Console.WriteLine("El FL indicado no es correcto");
                    Console.WriteLine("DEBUG: "+e.Message);
                    Console.ReadLine();
                }
                
            }

            return FL_IN;
        }

        /// <summary>
        /// Parte del menú de comando que interroga el FL, retorna null si experimenta algun error.
        /// </summary>
        /// <param name="FL_IN"></param>
        /// <returns></returns>
        public static string Comando_FL(string [] args)
        {
            string FL_IN = args[1];
            List<char> chars = new List<char>();
            bool FL_correcto = false;
            foreach (char c in FL_IN)
            {
                chars.Add(c);
            }

            try
            {
                long N = 0; //Comprobar que el FL es correcto, solo si lo es el programa seguira
                if ((chars[0] == 'F') && (chars[1] == 'L') && (long.TryParse(chars[2].ToString(), out N)) && (long.TryParse(chars[3].ToString(), out N)) && (long.TryParse(chars[4].ToString(), out N)))
                    FL_correcto = true;
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("El FL indicado no es correcto");
                    System.Threading.Thread.Sleep(2000);
                    Operaciones.EscribirOutput(args, "FL indicado no válido");
                    Console.Clear();
                }
                if (FL_correcto)
                    return FL_IN;
                else
                    return null;
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("El FL indicado no es correcto");
                Console.WriteLine("DEBUG: " + e.Message);
                Operaciones.EscribirOutput(args, "FL indicado no válido ("+e.Message+")");
                System.Threading.Thread.Sleep(2000);
                Console.Clear();
            }

            return null;
        }

        /// <summary>
        /// Parte del menú del cálculo de multi-cobertura que pregunta por el directorio de entrada
        /// </summary>
        /// <param name="FL"></param>
        /// <returns></returns>
        public static (DirectoryInfo, string) Menu_DirectorioIN(string FL, int mode) //Preguntar directiorio de entrada
        {
            Console.WriteLine();
            string Directorio_IN = null;
            DirectoryInfo DI = new DirectoryInfo(@".\Temporal");
            bool Correcto = false;
            while (!Correcto)
            {
                Console.WriteLine("Directorio de entrada, (no puede contener puntos (.))");
                Directorio_IN = Console.ReadLine();
                
                try
                {
                    DI = new DirectoryInfo(Directorio_IN);
                    int control = 0;
                    if (mode == 1)
                        control = DI.GetFiles().Count();
                    else
                    {
                        control = DI.GetDirectories().Length; //Para comprovar que hay directorios y no ficheros (cobertura mínima)
                        if(control==0)
                        {
                            control = DI.GetFiles().Count();
                        }
                    }
                        
                    if (control != 0)
                        Correcto = true;
                    else
                    {
                        if(mode == 1)
                        {
                            Console.WriteLine();
                            Console.WriteLine("Error con el directorio de entrada, no puede contener puntos (.)");
                            Console.WriteLine("DEBUG: Carpeta vacía");
                            Console.ReadLine();
                            Console.Clear();
                            Console.WriteLine("Berta T");
                            Console.WriteLine();
                            Console.WriteLine("1 - Cálculo de multi-coberturas");
                            Console.WriteLine();
                            Console.WriteLine("FL seleccionado: " + FL);
                            Console.WriteLine();
                        }
                        else
                        {
                            Console.WriteLine();
                            Console.WriteLine("Error con el directorio de entrada, no puede contener puntos (.)");
                            Console.WriteLine("DEBUG: Carpeta vacía");
                            Console.WriteLine();

                        }
                    }
                }
                catch (Exception e)
                {
                    if (mode == 1)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Error con el directorio de entrada, no puede contener puntos (.)");
                        Console.WriteLine("DEBUG: " + e.Message);
                        Console.Clear();
                        Console.WriteLine("Berta T");
                        Console.WriteLine();
                        Console.WriteLine("1 - Cálculo de multi-coberturas");
                        Console.WriteLine();
                        Console.WriteLine("FL seleccionado: " + FL);
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine("Error con el directorio de entrada, no puede contener puntos (.)");
                        Console.WriteLine("DEBUG: " + e.Message);
                        Console.WriteLine();
                    }
                }
            }

            return (DI, Directorio_IN);
        }

        /// <summary>
        /// Parte del menú de comando que interroga el directorio de entrada, retorna null si experimenta algun error.
        /// </summary>
        /// <param name="Directorio_IN"></param>
        /// <returns></returns>
        public static (DirectoryInfo, string) Comando_DirectorioIN(string[] args)
        {
            string Directorio_IN = "";
            if (Convert.ToInt32(args[0]) == 1)
                Directorio_IN = args[2];
            else
                Directorio_IN = args[1];
            DirectoryInfo DI = new DirectoryInfo(@".\Temporal");
            try
            {
                DI = new DirectoryInfo(Directorio_IN);
                int control = 0;
                if (Convert.ToInt32(args[0]) != 3)
                    control = DI.GetFiles().Count();
                else
                    control = DI.GetDirectories().Length;

                if (control != 0)
                    return (DI, Directorio_IN);
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("Error con el directorio de entrada, no puede contener puntos (.)");
                    Console.WriteLine("DEBUG: Carpeta vacía");
                    Operaciones.EscribirOutput(args, "Directorio de entrada no válido (Carpeta vacía)");
                    System.Threading.Thread.Sleep(2000);
                    Console.Clear();

                    return (null, null);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("Error con el directorio de entrada, no puede contener puntos (.)");
                Console.WriteLine("DEBUG: " + e.Message);
                Operaciones.EscribirOutput(args, "Directorio de entrada no válido (" + e.Message + ")");
                System.Threading.Thread.Sleep(2000);
                Console.Clear();

                return (null, null);
            }
        }

        /// <summary>
        /// Parte del menú del cálculo de multi-cobertura que pregunta por el umbral de discriminación
        /// </summary>
        /// <param name="Trans"></param>
        /// <returns></returns>
        public static double Menu_Umbral(double Trans) //Pregunta por el umbral
        {
            Console.WriteLine();
            Console.WriteLine("Introducir el umbral de discriminación de areas [NM^2] (Predeterminado: 1 NM^2)");
            string NM_Umbral = Console.ReadLine();
            double Umbral_Areas = 1 * Trans;
            if (NM_Umbral != "")
            {
                try
                {
                    Umbral_Areas = Convert.ToDouble(NM_Umbral) * Trans;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Formato incorrecto, se usará el umbral predeterminado (1 NM^2)");
                    Console.WriteLine("DEBUG: " + e.Message);
                    Console.ReadLine();
                }
            }

            return Umbral_Areas;
        }


        /// <summary>
        /// Parte del menú de comando que interroga el umbral de discriminacion, retorna -1 si experimenta algun error.
        /// </summary>
        /// <param name="Trans"></param>
        /// <param name="NM_Umbral"></param>
        /// <returns></returns>
        public static double Comando_Umbral(double Trans, string[] args)
        {
            try
            {
                double NM_Umbral = Convert.ToDouble(args[3].Replace('.', ','));
                double Umbral_Areas = NM_Umbral * Trans;
                return Umbral_Areas;
            }
            catch (Exception e)
            {
                Console.WriteLine("DEBUG: " + e.Message);
                Operaciones.EscribirOutput(args, "Error con umbral de discriminación (" + e.Message + ")");
                System.Threading.Thread.Sleep(2000);
                Console.Clear();
                return -1;
            }
        }

        /// <summary>
        /// Parte del menú del cálculo de multi-cobertura que pregunta por el directorio de salida
        /// </summary>
        /// <param name="Directorio_IN"></param>
        /// <param name="Umbral_Areas"></param>
        /// <param name="Trans"></param>
        /// <param name="NombresCargados"></param>
        /// <param name="Segs"></param>
        /// <param name="TiempoEjecución_Parte2"></param>
        /// <param name="NombreProyecto"></param>
        /// <returns></returns>
        public static string Menu_DirectorioOUT(string Directorio_IN, double Umbral_Areas, double Trans, List<string> NombresCargados, double Segs, TimeSpan TiempoEjecución_Parte2, string NombreProyecto) //Preguntar directiorio de entrada
        {
            Console.WriteLine();
            string Directorio_OUT = null;
            DirectoryInfo DO = new DirectoryInfo(@".\Temporal");
            bool Correcto = false;
            while (!Correcto)
            {
                Console.WriteLine("Directorio de salida, (no puede contener puntos (.))");
                Directorio_OUT = Console.ReadLine();
                try
                {
                    DO = new DirectoryInfo(Directorio_OUT);
                    DO.GetFiles().Count();
                    Correcto = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error con el directorio de salida, no puede contener puntos (.)");
                    Console.WriteLine("DEBUG: " + e.Message);
                    Console.ReadLine();
                    Console.Clear();
                    Console.WriteLine("Berta T");
                    Console.WriteLine();
                    Console.WriteLine("1 - Cálculo de multi-coberturas");
                    Console.WriteLine();
                    Console.WriteLine("Directorio de entrada: " + Directorio_IN);
                    Console.WriteLine();
                    Console.WriteLine("Umbral de discriminación: " + Math.Round(Umbral_Areas / Trans, 3) + " NM^2");
                    Console.WriteLine();
                    Console.WriteLine("Archivos cargados:");
                    Console.WriteLine();
                    foreach (string N in NombresCargados)
                        Console.WriteLine(N);
                    Console.WriteLine();
                    Console.WriteLine("Tiempo de ejecución: " + Math.Round(TiempoEjecución_Parte2.TotalSeconds / 60, 0) + " minutos " + Segs + " segundos");
                    Console.WriteLine();
                    Console.WriteLine("Nombre del proyecto: " + NombreProyecto);
                    Console.WriteLine();
                }
            }

            return Directorio_OUT;
        }

        /// <summary>
        /// Parte del menú que pregunta por el directorio de salida, MODO COBERTURA MÍNIMA
        /// </summary>
        /// <returns></returns>
        public static string Menu_DirectorioOUT() //Preguntar directiorio de entrada
        {
            Console.WriteLine();
            string Directorio_OUT = null;
            DirectoryInfo DO = new DirectoryInfo(@".\Temporal");
            bool Correcto = false;
            while (!Correcto)
            {
                Console.WriteLine("Directorio de salida, (no puede contener puntos (.))");
                Directorio_OUT = Console.ReadLine();
                try
                {
                    DO = new DirectoryInfo(Directorio_OUT);
                    DO.GetFiles().Count();
                    Correcto = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error con el directorio de salida, no puede contener puntos (.)");
                    Console.WriteLine("DEBUG: " + e.Message);
                    Console.ReadLine();
                    Console.Clear();
                }
            }

            return Directorio_OUT;
        }

        /// <summary>
        ///  Parte del menú de comando que interroga el directorio de salida, retorna null si experimenta algun error.
        /// </summary>
        /// <param name="Directorio_OUT"></param>
        /// <returns></returns>
        public static string Comando_DirectorioOUT(string Directorio_OUT)
        {
            DirectoryInfo DO = new DirectoryInfo(@".\Temporal");
            try
            {
                DO = new DirectoryInfo(Directorio_OUT);
                DO.GetFiles().Count();
                return Directorio_OUT;
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("Error con el directorio de salida, no puede contener puntos (.)");
                Console.WriteLine("DEBUG: " + e.Message);
                System.Threading.Thread.Sleep(2000);
                Console.Clear();
                return null;
            }
        }

        /// <summary>
        /// Parte del menú del filtrado SACTA que pregunta por el directorio de entrada
        /// </summary>
        /// <param name="FL"></param>
        /// <returns></returns>
        public static (DirectoryInfo, string) Menu_DirectorioIN_SACTA() //Preguntar directiorio de entrada
        {
            Console.WriteLine();
            string Directorio_IN = null;
            DirectoryInfo DI = new DirectoryInfo(@".\Temporal");
            bool Correcto = false;
            while (!Correcto)
            {
                Console.WriteLine("Directorio de entrada (no puede contener puntos (.))");
                Directorio_IN = Console.ReadLine();
                try
                {
                    DI = new DirectoryInfo(Directorio_IN);
                    int control = DI.GetFiles().Count();
                    if (control != 0)
                        Correcto = true;
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine("Error con el directorio de entrada, no contiene archivos");
                        Console.WriteLine("DEBUG: Carpeta vacía");
                        Console.WriteLine("Enter para continuar");
                        Console.ReadLine();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error con el directorio de entrada, no puede contener puntos (.)");
                    Console.WriteLine("DEBUG: " + e.Message);
                    Console.WriteLine("Enter para continuar");
                    Console.ReadLine();
                    Console.Clear();
                    Console.WriteLine("Berta T");
                    Console.WriteLine();
                    Console.WriteLine("2 - Filtrado SACTA");
                    Console.WriteLine();
                }
            }

            return (DI, Directorio_IN);
        }

        /// <summary>
        /// Parte del menú del filtrado SACTA que pregunta por el directorio de entrada del filtro
        /// </summary>
        /// <param name="NombresCargados"></param>
        /// <param name="path_Cob"></param>
        /// <returns></returns>
        public static (Cobertura, string) Menu_DirectorioSACTA_SACTA(List<string> NombresCargados, string path_Cob, int CvsM, string[] args)
        {
            Cobertura Filtro_SACTA = new Cobertura();
            string path_SACTA = "";
            bool Correcto = false;
            while (!Correcto)
            {
                Console.Clear();
                Console.WriteLine("Berta T");
                Console.WriteLine();
                Console.WriteLine("2 - Filtrado SACTA");
                Console.WriteLine();
                Console.WriteLine("Directorio de cobertura a filtrar: " + path_Cob);
                Console.WriteLine();
                Console.WriteLine("Archivos cargados:");
                foreach (string Nom in NombresCargados)
                {
                    Console.WriteLine(Nom);
                }
                Console.WriteLine();
                Console.WriteLine("Directorio completo del kmz con el filtro SACTA:");
                
                if (CvsM == 0)
                    path_SACTA = Console.ReadLine();
                else
                {
                    path_SACTA = args[2];
                    Correcto = true;
                }
                    

                try
                {
                    (FileStream H, string Nombre) = Operaciones.AbrirKMLdeKMZ(path_SACTA);
                    List<Geometry> Poligonos = Operaciones.TraducirPoligono(H, Nombre, args, CvsM, Nombre); //Carga kml, extrae en SharpKML y traduce a NTS
                    Filtro_SACTA = new Cobertura("Filtro", "FL999", "original", Poligonos); //Cobertura donde guardaremos el filtro SACTA seleccionado
                    if(Poligonos[0].IsEmpty==false) //Controlar error en traducción
                        Correcto = true;
                }
                catch (Exception e)
                {
                    Filtro_SACTA = null;
                    Console.WriteLine();
                    Console.WriteLine("Error con el directorio completo del kmz con el filtro SACTA, no puede contener puntos a excepción del archivo(.)");
                    Console.WriteLine("DEBUG: " + e.Message);
                    if(CvsM==0)
                    {
                        Console.WriteLine("Enter para continuar");
                        Console.ReadLine();
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(2000);
                        Operaciones.EscribirOutput(args, "Error directorio filtro SACTA (" + e + ")");
                    }
                    
                }
            }

            return (Filtro_SACTA,path_SACTA);
        }

        public static int Menu_DirectorioOUT_SACTA(string path_Cob, string path_SACTA, Conjunto Coberturas_Filtradas, int CvsM, string[] args)
        {
            //Confirmar directorio
            Console.WriteLine();
            string Directorio_OUT = null;
            DirectoryInfo DO = new DirectoryInfo(@".\Temporal");
            bool Correcto = false;
            bool errorFatal = false;
            while (!Correcto)
            {
                Console.WriteLine("Directorio de salida, (no puede contener puntos (.))");
                if(CvsM==0)
                    Directorio_OUT = Console.ReadLine();
                else
                {
                    Directorio_OUT = args[3];
                    Correcto = true;
                }
                try
                {
                    DO = new DirectoryInfo(Directorio_OUT);
                    DO.GetFiles().Count();
                    Correcto = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error con el directorio de salida, no puede contener puntos (.)");
                    Console.WriteLine("DEBUG: " + e.Message);
                    if(CvsM==0)
                    {
                        Console.WriteLine("Enter para continuar");
                        Console.ReadLine();
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(2000);
                        Operaciones.EscribirOutput(args, "Error directorio salida (" + e + ")");
                        errorFatal = true;
                    }
                    
                    Console.Clear();
                    Console.WriteLine("Berta T");
                    Console.Clear();
                    Console.WriteLine("Berta T");
                    Console.WriteLine();
                    Console.WriteLine("2 - Filtrado SACTA");
                    Console.WriteLine();
                    Console.WriteLine("Directorio de cobertura a filtrar: " + path_Cob);
                    Console.WriteLine();
                    Console.WriteLine("Directorio completo del kmz de filtros SACTA: " + path_SACTA);
                    Console.WriteLine();
                }
            }

            if (!errorFatal)
            {
                //Guardar coberturas
                int Pro = 0; int ProMax = Coberturas_Filtradas.A_Operar.Count;
                ProgressBar PB = new ProgressBar();
                foreach (Cobertura Cob in Coberturas_Filtradas.A_Operar)
                {
                    var doc = Cob.CrearDocumentoSharpKML();
                    string[] NombreSplit = doc.Name.Split(" ");
                    string Name = "";

                    if (NombreSplit.Length > 2) //Si hay mas de dos elementos, juntar primero el nombre y despues el FL
                    {
                        int i = 0; string[] NomesNom = new string[NombreSplit.Length - 1];
                        while (i < NombreSplit.Length - 1)
                        {
                            NomesNom[i] = NombreSplit[i];
                        }

                        Name = string.Join(' ', NomesNom);
                    }
                    else
                        Name = NombreSplit.First();

                    doc.Name = Name + "-" + NombreSplit.Last();

                    Operaciones.CrearKML_KMZ(doc, doc.Name, "Temporal", Directorio_OUT); //Se crea un kml temporal para después crear KMZ
                    PB.Report((double)Pro / ProMax);
                    Pro++;
                }
                PB.Dispose();
                return 0;
            }
            else
                return -1;
            
        }

        /// <summary>
        /// Guardo información de estado de un comando
        /// </summary>
        /// <param name="Argumentos"></param>
        /// <param name="Estado"></param>
        public static void EscribirOutput(string [] Argumentos, string Estado)
        {
            try
            {
                StreamReader R = new StreamReader("Output_COMMAND.txt");
                string Lin = R.ReadLine();
                List<string> Doc = new List<string>();
                while (Lin != null)
                {
                    Doc.Add(Lin);
                    Lin = R.ReadLine();
                }
                R.Close();

                string argumentos = string.Join(',', Argumentos);
                string DataHora = DateTime.Now.ToString();
                string[] NewLine = new string[3];
                NewLine[0] = DataHora;
                NewLine[1] = argumentos;
                NewLine[2] = Estado;
                string NewLineString = string.Join(' ', NewLine);
                Doc.Add(NewLineString);

                StreamWriter W = new StreamWriter("Output_COMMAND.txt");
                foreach (string lin in Doc)
                    W.WriteLine(lin);
                W.Close();
            }
            catch
            {
                Console.WriteLine("Fichero Output_COMMAND.txt no encontrado");
            }
            
        }

        public static List<Conjunto> CargarCoberturas_CoberturaMinima(string[] args, int CvsM)
        {
            return null;
        }
    }
}

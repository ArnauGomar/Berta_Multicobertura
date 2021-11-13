using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Precision;
using SharpKml.Engine;


namespace Berta
{
    
    class Program
    {
        static void Main(string[] args)
        {
            //Settings del NetTopologySuite para evitar errores de NaN en ciertos cálculos 
            //https://stackoverflow.com/questions/68035230/nettopology-found-non-noded-intersection-exception-when-determining-the-differ

            var curInstance = NetTopologySuite.NtsGeometryServices.Instance;
            NetTopologySuite.NtsGeometryServices.Instance = new NetTopologySuite.NtsGeometryServices(
                curInstance.DefaultCoordinateSequenceFactory,
                curInstance.DefaultPrecisionModel,
                curInstance.DefaultSRID,
                GeometryOverlay.NG, // RH: use 'Next Gen' overlay generator
                curInstance.CoordinateEqualityComparer);

            //Eliminar todos los posibles archivos existentes en carpeta temporales (esto puede ocurrir si se ha cerrado el programa antes de tiempo o a sucedido una excepción no deseada)
            DirectoryInfo TemporalC = new DirectoryInfo(@"Temporal"); 
            foreach (var file in TemporalC.GetFiles())
            {
                File.Delete(file.FullName);
            }

            int Control_M = -1;
            while (Control_M != 0) //Menú principal
            {
                //try
                {
                    Console.Clear();
                    Console.WriteLine("Berta M");
                    Console.WriteLine();
                    Console.WriteLine("1 - Cálculo de multicoberturas");
                    Console.WriteLine("2 - Filtrado SACTA");
                    Console.WriteLine("3 - Cálculo de redundáncias");
                    Console.WriteLine("4 - Cálculo de cobertura mínima");
                    Console.WriteLine("5 - Opciones del programa");
                    Console.WriteLine();
                    Console.WriteLine("0 - Finalizar");
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine("Introduzca identificador de operación (p.e. 1)");
                    Control_M = Convert.ToInt32(Console.ReadLine()); //Actualizar valor Control_M

                    //Ifs de control
                    if (Control_M == 1) //Cáclulo de multicobertura
                    {
                        List<SharpKml.Dom.Folder> Folders = new List<SharpKml.Dom.Folder>(); //Lista con archivos base kml para crear kmz final, donde se guardaran todas las carpetas con los resultados
                        SharpKml.Dom.Document KML_Cobertura_total = new SharpKml.Dom.Document(); //La cobertura total se guarda en un documento, no en una carpeta, por eso es una variable independiente

                        //Variables de información al usuario
                        string NombrePredeterminado = ""; //String para guardar un nombre de proyecto predeterminado
                        string Directorio_IN = ""; //Directorio de entrada
                        List<string> NombresCargados = new List<string>(); //Lista donde se guardan los nombres de los archivos cargados.
                        TimeSpan TiempoEjecución_Parte2 = new TimeSpan(); //Variable para guardar el tiempo de ejecución. 

                        bool parte1 = false; //Control de parte1, si no se ha ejecutado correctamente la parte 1 la parte 2 no sucede.
                        //try //Parte1 - Cargar ficheros de entrada y ejecutar cálculos
                        {
                            //Parte1 - Cargar ficheros de entrada y ejecutar cálculos
                            //1.1 - FL
                            bool FL_correcto = false;
                            string FL_IN = "Error";
                            while (!FL_correcto)
                            {
                                Console.Clear();
                                Console.WriteLine("EXCAMUB");
                                Console.WriteLine();
                                Console.WriteLine("1 - Cálculo de multicoberturas");
                                Console.WriteLine();
                                Console.WriteLine("FL seleccionado (p.e.: FL100 / FL090):");

                                //Obtener el FL
                                FL_IN = Console.ReadLine();
                                List<char> chars = new List<char>();
                                foreach (char c in FL_IN)
                                {
                                    chars.Add(c);
                                }

                                long N = 0; //Comprobar que el FL es correcto, solo si lo es el programa seguira
                                if ((chars[0] == 'F') && (chars[1] == 'L') && (long.TryParse(chars[2].ToString(), out N)) && (long.TryParse(chars[3].ToString(), out N)) && (long.TryParse(chars[4].ToString(), out N)))
                                    FL_correcto = true;
                                else
                                {
                                    Console.WriteLine();
                                    Console.WriteLine("El FL indicado no es correcto");
                                }
                                Console.WriteLine();
                            }

                            //1.2 - Entrada
                            Console.WriteLine("Directorio de entrada");
                            Directorio_IN = Console.ReadLine();
                            Console.WriteLine();
                            DirectoryInfo DI = new DirectoryInfo(Directorio_IN); //Abrimos carpeta in para leer archivos

                            //1.3 - Cargar archivos
                            Console.WriteLine("Archivos cargados:");

                            List<Cobertura> Originales = new List<Cobertura>(); //lista de coberturas para guardar las coberturas cargadas y despues crear un conjunto
                            if(DI.GetFiles().Count()>1) //Si hay mas de 1 archivo dentro de la carpeta se ejecuta el programa
                            {
                                foreach (var file in DI.GetFiles())
                                {
                                    //Abrir KML
                                    System.IO.Compression.ZipFile.ExtractToDirectory(file.FullName, @".\Temporal"); //extraer KML
                                    FileStream H;
                                    string FileName; string Nombre;
                                    string FL;
                                    if (File.Exists(Path.Combine(@".\Temporal", file.Name.Split(".")[0] + ".kml")))
                                    {
                                        H = File.Open(Path.Combine(@".\Temporal", file.Name.Split(".")[0] + ".kml"), FileMode.Open); //Abrir KML  
                                        FileName = file.Name.Split(".")[0];
                                        Nombre = FileName;
                                    }

                                    else
                                    {
                                        H = File.Open(Path.Combine(@".\Temporal", "doc.kml"), FileMode.Open); //Abrir KML generico 
                                        FileName = "doc";
                                        Nombre = file.Name.Split(".")[0];
                                        FL = file.Name.Split(".")[0].Split('-')[1];
                                    }

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

                                    Originales.Add(new Cobertura(Nombre.Split('-')[0], FL_IN, "original", Poligonos));

                                    Console.WriteLine(Nombre);
                                    NombresCargados.Add(Nombre);
                                }

                                //1.4 - Cálculos
                                //Originales (crear carpeta)
                                Conjunto conjunto = new Conjunto(Originales, "original", FL_IN);

                                //Flitrar permutaciones 
                                Console.WriteLine();
                                Console.WriteLine("Inicio del cáclulo...");
                                Stopwatch stopwatch = Stopwatch.StartNew(); //Reloj para conocer el tiempo de ejecución
                                conjunto.GenerarListasIntersecciones();
                                conjunto.FiltrarCombinaciones(); //Eliminamos las combinaciones que no van a generar una intersección
                                                                 //conjunto.FiltrarCombinaciones_Experimental();
                                stopwatch.Stop();

                                Console.WriteLine();
                                Console.WriteLine("Tiempo de ejecución primera parte: " + new TimeSpan(stopwatch.ElapsedTicks).TotalSeconds + " segundos");

                                //Mostrar en consola un tiempo estimado de cálculo
                                double NumMuestras = conjunto.Combinaciones.Count();
                                double MuestraSegundo = 163.371;
                                double tiempo = (NumMuestras / MuestraSegundo) / 60;
                                Console.WriteLine("Se espera que el programa termine en unos " + Math.Round(tiempo, 0) + " minutos (" + DateTime.Now.ToString() + ")");

                                stopwatch = Stopwatch.StartNew(); //Iniciamos el cronometro otra vez
                                var folder_BASE = new SharpKml.Dom.Folder { Id = "Coberturas-Base", Name = "Coberturas Base " + FL_IN, }; //Creamos carpeta donde guardaremos todos los documentos relacionados
                                folder_BASE.Visibility = false; //Iniciado de forma no visibile (no tick en google earth)

                                foreach (Cobertura COB in conjunto.A_Operar)
                                {
                                    folder_BASE.AddFeature(COB.CrearDocumentoSharpKML());
                                }

                                Folders.Add(folder_BASE);

                                //Cobertura total (Documento)
                                Cobertura CoberturaTotal = conjunto.FormarCoberturaTotal();
                                KML_Cobertura_total = CoberturaTotal.CrearDocumentoSharpKML(); //Documento KML de la cobertura total

                                //Coberturas multiples y multiples total
                                (List<Conjunto> Listado_ConjuntoCoberturasMultiples, Conjunto Anillos, Cobertura CoberturaMaxima) = conjunto.FormarCoberturasMultiples(); //Cálculo coberturas múltiples

                                //Coberturas simples
                                (Conjunto CoberturasSimples, Cobertura CoberturaMultipleTotal, Cobertura CoberturaSimpleTotal) = conjunto.FormarCoberturasSimples(Anillos, CoberturaMaxima); //Cálculo coberturas simples

                                //Añadir coberturas simples (crear carpeta)
                                var folder_Simples = new SharpKml.Dom.Folder();
                                folder_Simples.Visibility = false;
                                folder_Simples.Name = "Multi-Cobertura " + string.Format("{0:00}", 1) + " " + FL_IN;
                                folder_Simples.Id = "Multi-Cobertura-" + string.Format("{0:00}", 1);
                                folder_Simples.AddFeature(CoberturaSimpleTotal.CrearDocumentoSharpKML());
                                foreach (Cobertura COB in CoberturasSimples.A_Operar)
                                {
                                    folder_Simples.AddFeature(COB.CrearDocumentoSharpKML());
                                }
                                Folders.Add(folder_Simples);

                                //Añadimos coberturas múltiples y anillos (crear carpeta)
                                foreach (Conjunto con in Listado_ConjuntoCoberturasMultiples)
                                {
                                    int Lvl = Convert.ToInt32(con.Identificador.Split(" ")[1]);
                                    var folder_lvl = new SharpKml.Dom.Folder();
                                    folder_lvl.Visibility = false;
                                    folder_lvl.Name = "Multi-Cobertura " + string.Format("{0:00}", Lvl) + " " + FL_IN;
                                    folder_lvl.Id = "Multi-Cobertura-" + string.Format("{0:00}", Lvl);

                                    //Buscar anillo correspondiente al lvl 
                                    var anillo = Anillos.A_Operar.Where(x => x.tipo_multiple == Lvl).ToList()[0];
                                    folder_lvl.AddFeature(anillo.CrearDocumentoSharpKML());

                                    //Añadir multicoberturas
                                    foreach (Cobertura cob in con.A_Operar)
                                        folder_lvl.AddFeature(cob.CrearDocumentoSharpKML());

                                    Folders.Add(folder_lvl);
                                }

                                //Añadimos cobertura máxima si es que existe (crear carpeta)
                                if (CoberturaMaxima != null)
                                {
                                    var folder_MAX = new SharpKml.Dom.Folder();
                                    folder_MAX.Visibility = false;
                                    int Lvl = CoberturaMaxima.tipo_multiple;
                                    folder_MAX.Name = "Multi-Cobertura " + string.Format("{0:00}", Lvl) + " " + FL_IN;
                                    folder_MAX.Id = "Multi-Cobertura-" + string.Format("{0:00}", Lvl);
                                    folder_MAX.AddFeature(CoberturaMaxima.CrearDocumentoSharpKML());

                                    Folders.Add(folder_MAX);
                                }

                                if (NombresCargados.Count < 20)
                                    NombrePredeterminado = String.Join('.', NombresCargados);
                                else
                                    NombrePredeterminado = NombresCargados[0] + "." + NombresCargados.Last() + " (entre otros)";

                                stopwatch.Stop();
                                TiempoEjecución_Parte2 = new TimeSpan(stopwatch.ElapsedTicks);
                                parte1 = true;
                                //Fin de la parte1
                            }
                            else
                            {
                                Console.WriteLine();
                                Console.WriteLine("Esta carpeta no contiene suficientes archivos para realizar los cálculos");
                                Console.ReadLine();
                            }
                        }
                        //catch (Exception e)
                        //{
                        //    Console.WriteLine(e.Message);
                        //    Console.ReadLine()
                        //} //Parte1 - Cargar ficheros de entrada, FL y ejecutar cálculos

                        if (parte1) //Si y solo si la parte1 ha sido ejecutada con éxito ejecutamos la parte2
                        {
                            //Parte2 - Guardar fichero en carpeta salida, obtener nombre de proyecto

                            //Informar al usuario
                            Console.Clear();
                            Console.WriteLine("EXCAMUB");
                            Console.WriteLine();
                            Console.WriteLine("1 - Cálculo de multicoberturas");
                            Console.WriteLine();
                            Console.WriteLine("Directorio de entrada: " + Directorio_IN);
                            Console.WriteLine();
                            Console.WriteLine("Archivos cargados:");
                            Console.WriteLine();
                            foreach (string N in NombresCargados)
                                Console.WriteLine(N);
                            Console.WriteLine();
                            double Segs = Math.Round((Math.Round(TiempoEjecución_Parte2.TotalSeconds / 60, 2) - Math.Round(TiempoEjecución_Parte2.TotalSeconds / 60, 0))*60,0);
                            Console.WriteLine("Tiempo de ejecución: " + Math.Round(TiempoEjecución_Parte2.TotalSeconds/60,0) +" minutos " + Segs + " segundos");
                            Console.WriteLine();

                            //2.1 - Nombre de proyecto
                            Console.WriteLine("Introduzca nombre del proyecto (si no introduce ninguno se creara uno por defecto):");
                            string NombreProyecto = Console.ReadLine();
                            if (NombreProyecto == "") //Nombre por defecto
                            {
                                NombreProyecto = NombrePredeterminado;
                            }

                            //2.2 - Crear documento para exportar
                            var Doc = new SharpKml.Dom.Document(); //se crea documento
                            Doc.Name = NombreProyecto;

                            //Ordenar carpetas tal y como enaire lo quiere
                            Folders= Folders.OrderBy(x=>x.Name).ToList();

                            Doc.AddFeature(KML_Cobertura_total); //Añadimos cobertura total
                            foreach (SharpKml.Dom.Folder fold in Folders)
                            {
                                Doc.AddFeature(fold); //añadir placermak dentro del documento
                            }

                            int Control_CM_Parte2 = -1;
                            while (Control_CM_Parte2 != 0)
                            {
                                //Informar al usuario
                                Console.Clear();
                                Console.WriteLine("EXCAMUB");
                                Console.WriteLine();
                                Console.WriteLine("1 - Cálculo de multicoberturas");
                                Console.WriteLine();
                                Console.WriteLine("Directorio de entrada: " + Directorio_IN);
                                Console.WriteLine();
                                Console.WriteLine("Archivos cargados:");
                                Console.WriteLine();
                                foreach (string N in NombresCargados)
                                    Console.WriteLine(N);
                                Console.WriteLine();
                                double Segs2 = Math.Round((Math.Round(TiempoEjecución_Parte2.TotalSeconds / 60, 2) - Math.Round(TiempoEjecución_Parte2.TotalSeconds / 60, 0)) * 60, 0);
                                Console.WriteLine("Tiempo de ejecución: " + Math.Round(TiempoEjecución_Parte2.TotalSeconds / 60, 0) + " minutos " + Segs + " segundos");
                                Console.WriteLine();
                                Console.WriteLine("Nombre del proyecto: " + NombreProyecto);
                                Console.WriteLine();
                                Console.WriteLine();

                                //2.3 - Directorio de salida
                                Console.WriteLine("Directorio de salida");
                                string Directorio_OUT = Console.ReadLine();
                                Console.WriteLine();

                                //2.4 - Exportar proyecto
                                int Control = Operaciones.CrearKML_KMZ(Doc, NombreProyecto, "Temporal", Directorio_OUT); //Se crea un kml temporal para después crear KMZ
                                if (Control == 0)
                                {
                                    Console.WriteLine("Exportado con exito!");
                                    Console.WriteLine();
                                    Console.WriteLine("Nombre del archivo: " + NombreProyecto + ".kmz");
                                    Console.ReadLine();
                                    Control_CM_Parte2 = 0; //Finalizar bucle
                                }
                                else
                                {
                                    Console.WriteLine("Directorio de destino no válido");
                                    Console.WriteLine();
                                    Console.WriteLine("Enter para continuar");
                                    Console.ReadLine();
                                }
                            }
                        } //Parte2 - Guardar fichero en carpeta salida, obtener nombre de proyecto
                    }//Cáclulo de multicobertura
                    else if (Control_M == 5)//Opciones del programa
                    {
                        
                    }//Opciones del programa
                }
                //catch (FormatException e) //Detectar errores sobre el Control_M
                //{
                //    Console.WriteLine(e.Message);
                //    Console.WriteLine();
                //    Console.WriteLine("Enter para continuar");
                //    Console.ReadLine();
                //    Control_M = -1; //Sigue el buclce
                //}
            }
        }
    }
}

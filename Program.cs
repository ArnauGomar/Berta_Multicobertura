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
            foreach (System.IO.FileInfo file in TemporalC.GetFiles()) file.Delete();
            foreach (System.IO.DirectoryInfo subDirectory in TemporalC.GetDirectories()) subDirectory.Delete(true);

            //Metodo comando vs Metodo menú 0 Menú 1 Comandos
            StreamReader CvsM_R = new StreamReader("Ajustes.txt");
            int CvsM = Convert.ToInt32(CvsM_R.ReadLine());
            Console.WriteLine(CvsM);

            //Obtener tolerancias y declarar umbrales
            double epsilon = Convert.ToDouble(CvsM_R.ReadLine());
            double Trans = 0.0003636658613823218; //1 NM^2 Valor empirico
            double Umbral_Areas = 1 * Trans;
            Console.WriteLine(epsilon);
            double epsilon_simple = Convert.ToDouble(CvsM_R.ReadLine());
            Console.WriteLine(epsilon_simple);

            CvsM_R.Close();

            int Control_M = -1;
            List<string> comando = new List<string>();
            while (Control_M != 0) //Menú principal
            {
                try
                {
                    Console.Clear();
                    Console.WriteLine("Berta T");
                    Console.WriteLine();
                    Console.WriteLine("1 - Cálculo de multi-coberturas");
                    Console.WriteLine("2 - Filtrado SACTA");
                    Console.WriteLine("3 - Cálculo de cobertura mínima");
                    Console.WriteLine("5 - Ajustes");
                    Console.WriteLine();
                    Console.WriteLine("0 - Finalizar");
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine("Introduzca identificador de operación (p.e. 1)");
                    if(args.Length ==0) //No pasamos nada des de consola superior
                    {
                        if (CvsM == 0) //Modo menú
                            Control_M = Convert.ToInt32(Console.ReadLine()); //Actualizar valor Control_M
                        else//Modo comando
                        {
                            string lin = Console.ReadLine();
                            if (lin != "0") //No cerramos aplicación
                            {
                                comando = lin.Split(',').ToList();
                                Control_M = Convert.ToInt32(comando[0]);
                            }
                            else
                                Control_M = 0; //Cerramos aplicación

                        }//Modo comando
                    } //No pasamos nada des de consola superior
                    else //Comando por consola superior/extrena
                    {

                    }
                    

                    //Ifs de control
                    if (Control_M == 1) //Cáclulo de multicobertura
                    {
                        List<SharpKml.Dom.Folder> Folders = new List<SharpKml.Dom.Folder>(); //Lista con archivos base kml para crear kmz final, donde se guardaran todas las carpetas con los resultados
                        SharpKml.Dom.Document KML_Cobertura_total = new SharpKml.Dom.Document(); //La cobertura total se guarda en un documento, no en una carpeta, por eso es una variable independiente

                        SharpKml.Dom.Folder Redundados = new SharpKml.Dom.Folder(); //Carpeta donde se guardaran los resultados radar a radar

                        //Variables de información al usuario
                        string NombrePredeterminado = ""; //String para guardar un nombre de proyecto predeterminado
                        string Directorio_IN = ""; //Directorio de entrada
                        List<string> NombresCargados = new List<string>(); //Lista donde se guardan los nombres de los archivos cargados.
                        TimeSpan TiempoEjecución_Parte2 = new TimeSpan(); //Variable para guardar el tiempo de ejecución. 

                        bool parte1 = false; //Control de parte1, si no se ha ejecutado correctamente la parte 1 la parte 2 no sucede.
                        //try //Parte1 - Cargar ficheros de entrada y ejecutar cálculos
                        {
                            //Parte1 - Cargar ficheros de entrada y ejecutar cálculos

                            DirectoryInfo DI = null;
                            string FL_IN = "Error";
                            if (CvsM == 0) //Modo menú
                            {
                                //1.1 - FL
                                FL_IN = Operaciones.Menu_FL();

                                //1.2 - Entrada
                                (DI, Directorio_IN) = Operaciones.Menu_DirectorioIN(FL_IN);

                                //1.2.2 Entrada umbral de filtro (1NM de forma predeterminada)
                                Umbral_Areas = Operaciones.Menu_Umbral(Trans);

                            }//Modo menú
                            else//Modo comando
                            {
                                //1.1 - FL
                                FL_IN = comando[1];
                                bool FL_correcto = false;
                                while (!FL_correcto)
                                {
                                    Console.Clear();
                                    
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
                                        Console.Clear();
                                        Console.WriteLine("Berta T");
                                        Console.WriteLine();
                                        Console.WriteLine("1 - Cálculo de multi-coberturas");
                                        Console.WriteLine();
                                        Console.WriteLine("El FL indicado no es correcto, introduzca uno correcto");
                                        Console.WriteLine();
                                        Console.WriteLine("FL seleccionado (p.e.: FL100 / FL090):");
                                        FL_IN = Console.ReadLine();
                                    }
                                }
                            }//Modo comando

                            //1.3 - Cargar archivos
                            
                            Console.WriteLine("Archivos cargados:");
                            if(DI.GetFiles().Count()>1) //Si hay mas de 1 archivo dentro de la carpeta se ejecuta el programa
                            {
                                //Cargar coberturas originales 
                                List<Cobertura> Originales = new List<Cobertura>();
                                (Originales, NombresCargados) = Operaciones.CargarCoberturas(DI, FL_IN);
                                
                                //1.4 - Cálculos
                                //Originales (crear carpeta)
                                Conjunto conjunto = new Conjunto(Originales, "original", FL_IN);

                                //Flitrar permutaciones 
                                Console.WriteLine();
                                Console.WriteLine("Inicio del cáclulo...");
                                Stopwatch stopwatch = Stopwatch.StartNew(); //Reloj para conocer el tiempo de ejecución
                                //conjunto.GenerarListasIntersecciones();
                                conjunto.FiltrarCombinaciones(); //Eliminamos las combinaciones que no van a generar una intersección
                                                                 //conjunto.FiltrarCombinaciones_Experimental();
                                stopwatch.Stop();

                                Console.WriteLine();
                                Console.WriteLine("Tiempo de ejecución primera parte: " + new TimeSpan(stopwatch.ElapsedTicks).TotalSeconds + " segundos");

                                //Mostrar en consola un tiempo estimado de cálculo
                                double NumMuestras = conjunto.Combinaciones.Count();
                                double MuestraSegundo = 163.371; //Valor empirico
                                double tiempo = (NumMuestras / MuestraSegundo) / 60;
                                double segundos = Math.Round(((Math.Round(tiempo, 2) - Math.Round(tiempo, 0)) * 60)+25,0);
                                if (segundos < 0)
                                    segundos = 0;
                                Console.WriteLine("Se espera que el programa termine en unos " + Math.Round(tiempo, 0) + " minutos "+segundos+" segundos (" + DateTime.Now.ToString() + ")");
                                Console.WriteLine();
                                Console.WriteLine("Calculando...");

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
                                (List<Conjunto> Listado_ConjuntoCoberturasMultiples, Conjunto Anillos, Cobertura CoberturaMaxima) = conjunto.FormarCoberturasMultiples(epsilon, Umbral_Areas); //Cálculo coberturas múltiples

                                //Coberturas simples
                                (Conjunto CoberturasSimples, Cobertura CoberturaMultipleTotal, Cobertura CoberturaSimpleTotal) = conjunto.FormarCoberturasSimples(Anillos, CoberturaMaxima, epsilon_simple, Umbral_Areas); //Cálculo coberturas simples

                                //FILTRAR AQUI ANILLOS (EVITAR ERRORES EN COBERTURA SIMPLE, POLIGONOS NO VÁLIDOS)
                                if(Anillos!=null)
                                {
                                    foreach (Cobertura anillo in Anillos.A_Operar)
                                    {
                                        List<Polygon> Verificados = new List<Polygon>(); //Lista de verificación de poligonos
                                        var gff = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(); //Factoria para crear multipoligonos
                                        if (anillo.Area_Operaciones.GetType().ToString() == "NetTopologySuite.Geometries.MultiPolygon")
                                        {
                                            foreach (Polygon TrozoAnillo in (MultiPolygon)anillo.Area_Operaciones)
                                            {
                                                if (TrozoAnillo.Area >= Umbral_Areas)
                                                    Verificados.Add(TrozoAnillo);
                                            }
                                            anillo.ActualizarAreas(gff.CreateMultiPolygon(Verificados.ToArray()));
                                        }
                                    }
                                }
                                

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
                                    if(Anillos!=null)
                                    {
                                        var anillo = Anillos.A_Operar.Where(x => x.tipo_multiple == Lvl).ToList()[0];
                                        folder_lvl.AddFeature(anillo.CrearDocumentoSharpKML());
                                    }
                                    
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

                                if (NombresCargados.Count < 10)
                                    NombrePredeterminado = String.Join('.', NombresCargados);
                                else
                                    NombrePredeterminado = NombresCargados[0] + "." + NombresCargados.Last() + " (entre otros)";

                                //Redundados
                                Redundados = Operaciones.CarpetaRedundados(conjunto, CoberturasSimples, Listado_ConjuntoCoberturasMultiples, CoberturaMaxima);

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
                        //    Console.ReadLine();
                        //} //Parte1 - Cargar ficheros de entrada, FL y ejecutar cálculos

                        if (parte1) //Si y solo si la parte1 ha sido ejecutada con éxito ejecutamos la parte2
                        {
                            //Parte2 - Guardar fichero en carpeta salida, obtener nombre de proyecto

                            //Informar al usuario
                            Console.Clear();
                            Console.WriteLine("Berta T");
                            Console.WriteLine();
                            Console.WriteLine("1 - Cálculo de multi-coberturas");
                            Console.WriteLine();
                            Console.WriteLine("Directorio de entrada: " + Directorio_IN);
                            Console.WriteLine();
                            Console.WriteLine("Umbral de discriminación: "+ Math.Round(Umbral_Areas/Trans,3)+" NM^2");
                            Console.WriteLine();
                            Console.WriteLine("Archivos cargados:");
                            Console.WriteLine();
                            foreach (string N in NombresCargados)
                                Console.WriteLine(N);
                            Console.WriteLine();
                            double Segs = Math.Round((Math.Round(TiempoEjecución_Parte2.TotalSeconds / 60, 2) - Math.Round(TiempoEjecución_Parte2.TotalSeconds / 60, 0))*60,0);
                            if (Segs < 0)
                                Segs = 0;
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
                            Doc.AddFeature(Redundados); //añadimos carpeta redundados;

                            int Control_CM_Parte2 = -1;
                            while (Control_CM_Parte2 != 0)
                            {
                                //Informar al usuario
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
                                double Segs2 = Math.Round((Math.Round(TiempoEjecución_Parte2.TotalSeconds / 60, 2) - Math.Round(TiempoEjecución_Parte2.TotalSeconds / 60, 0)) * 60, 0);
                                Console.WriteLine("Tiempo de ejecución: " + Math.Round(TiempoEjecución_Parte2.TotalSeconds / 60, 0) + " minutos " + Segs + " segundos");
                                Console.WriteLine();
                                Console.WriteLine("Nombre del proyecto: " + NombreProyecto);
                                Console.WriteLine();

                                //2.3 - Directorio de salida
                                string Directorio_OUT = Operaciones.Menu_DirectorioOUT(Directorio_IN, Umbral_Areas, Trans, NombresCargados, Segs, TiempoEjecución_Parte2, NombreProyecto);
                                //Console.WriteLine("Directorio de salida");
                                //string Directorio_OUT = Console.ReadLine();
                                //Console.WriteLine();

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
                                    Console.WriteLine("Directorio de destino no válido, no puede contener puntos (.)");
                                    Console.WriteLine();
                                    Console.WriteLine("Enter para continuar");
                                    Console.ReadLine();
                                }
                            }
                        } //Parte2 - Guardar fichero en carpeta salida, obtener nombre de proyecto
                    }//Cáclulo de multicobertura
                    else if (Control_M == 2) //Filtrado SACTA
                    {
                        try
                        {
                            Console.Clear();
                            Console.WriteLine("Berta T");
                            Console.WriteLine();
                            Console.WriteLine("2 - Filtrado SACTA");
                            //Console.WriteLine();
                            //Console.WriteLine("Directorio de cobertura a filtrar:");
                            //string path_Cob = Console.ReadLine();
                            //DirectoryInfo Directorio_Cobertura = new DirectoryInfo(path_Cob);

                            (DirectoryInfo Directorio_Cobertura, string path_Cob) = Operaciones.Menu_DirectorioIN_SACTA();

                            //Si solo hay un solo archivo en la carpeta se cargará ese, sinó se le dara al usuarió la oportunidad de elegir cual.
                            //También puede seleccionar la opción de ejecutar el filtro sobre todos los archivos de la carpeta

                            List<Cobertura> Coberturas = new List<Cobertura>();
                            List<string> NombresCargados = new List<string>();

                            //Cargar todos los archivos de la carpeta
                            (Coberturas, NombresCargados) = Operaciones.CargarCoberturas(Directorio_Cobertura, null);

                            //Actualizar nombres en coberturas (FL)
                            int i = 0;
                            foreach(Cobertura cob in Coberturas)
                            {
                                cob.FL = NombresCargados[i].Split('-')[1];
                                i++;
                            }

                            if (Directorio_Cobertura.GetFiles().Count()>=1) //Minimo un archivo
                            {
                                bool Control_DC = false; //Bucle para cargar
                                while (Control_DC==false)
                                {
                                    try
                                    {
                                        Console.Clear();
                                        Console.WriteLine("Berta T");
                                        Console.WriteLine();
                                        Console.WriteLine("2 - Filtrado SACTA");
                                        Console.WriteLine();
                                        Console.WriteLine("Directorio de cobertura a filtrar: " + path_Cob);
                                        Console.WriteLine();
                                        Console.WriteLine();
                                        Console.WriteLine("Archivos en carpeta:");
                                        Console.WriteLine();
                                        i = 1;
                                        foreach (var file in Directorio_Cobertura.GetFiles())
                                        {
                                            Console.WriteLine("" + i + " - " + file.Name);
                                            i++;
                                        }
                                        Console.WriteLine();
                                        Console.WriteLine("¿Desea ejecutar el filtrado en todos los kmz dentro de la carpeta? (0 para si)");
                                        Console.WriteLine("Si solo desea ejecutar el fitrado sobre un solo kmz introduzca el identificador");
                                        Console.WriteLine("-1 para cambiar de directorio");
                                        int Control_DC_n = Convert.ToInt32(Console.ReadLine());

                                        if(Control_DC_n == 0) //Ejecutar toda la carpeta
                                        {
                                            Control_DC = true; //terminamos bucle
                                        }
                                        else if (Control_DC_n == -1) //Cambiamos el directorio
                                        {
                                            Console.WriteLine();
                                            Console.WriteLine("Directorio de cobertura a filtrar:");
                                            //path_Cob = Console.ReadLine();
                                            //Directorio_Cobertura = new DirectoryInfo(path_Cob);
                                            (Directorio_Cobertura,  path_Cob) = Operaciones.Menu_DirectorioIN_SACTA();
                                            (Coberturas, NombresCargados) = Operaciones.CargarCoberturas(Directorio_Cobertura, null); //Cargamos coberturas de los archivos de esa carpeta
                                            //Seguimos con el bucle
                                        }
                                        else if (Control_DC_n - 1 <= Directorio_Cobertura.GetFiles().Count())//Archivo en concreto Si el Control_DC_n es un archivo válido
                                        {
                                            Control_DC = true; //terminamos bucle
                                            //Extraemos info de indice seleccionado
                                            List<Cobertura> Seleccionada = new List<Cobertura>();
                                            Seleccionada.Add(Coberturas[Control_DC_n - 1]);
                                            Coberturas = Seleccionada;
                                            List<string> Seleccionado = new List<string>();
                                            Seleccionado.Add(NombresCargados[Control_DC_n - 1]);
                                            NombresCargados = Seleccionado;
                                        }
                                        else //Identificador no válido
                                        {
                                            Console.WriteLine();
                                            Console.WriteLine("Identificador no válido");
                                            Console.ReadLine();
                                            //Seguimos el bucle
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(e.Message);
                                        Console.WriteLine();
                                        Console.WriteLine("Enter para continuar");
                                        Console.ReadLine();
                                    }
                                }
                            }

                            Conjunto conjuntoAfiltrar = new Conjunto(Coberturas, "original", "FL999");
                            try
                            {
                                //Obtenemos el filtro SACTA
                                //Console.Clear();
                                //Console.WriteLine("Berta T");
                                //Console.WriteLine();
                                //Console.WriteLine("2 - Filtrado SACTA");
                                //Console.WriteLine();
                                //Console.WriteLine("Directorio de cobertura a filtrar: " + path_Cob);
                                //Console.WriteLine();
                                //Console.WriteLine("Archivos cargados:");
                                //foreach (string Nom in NombresCargados)
                                //{
                                //    Console.WriteLine(Nom);
                                //}
                                //Console.WriteLine();
                                //Console.WriteLine("Directorio completo del kmz con el filtro SACTA:");
                                //string path_SACTA = Console.ReadLine();

                                ////Abrir KML
                                //(FileStream H, string Nombre) = Operaciones.AbrirKMLdeKMZ(path_SACTA);
                                //List<Geometry> Poligonos = Operaciones.TraducirPoligono(H, Nombre); //Carga kml, extrae en SharpKML y traduce a NTS
                                //Cobertura Filtro_SACTA = new Cobertura("Filtro", "FL999", "original", Poligonos); //Cobertura donde guardaremos el filtro SACTA seleccionado

                                //Cargar filtro SACTA
                                (Cobertura Filtro_SACTA, string path_SACTA) = Operaciones.Menu_DirectorioSACTA_SACTA(NombresCargados, path_Cob);

                                //Ejecutar filtrado
                                Conjunto Filtrado = conjuntoAfiltrar.Aplicar_SACTA(Filtro_SACTA);

                                //Obtener directorio de salida
                                Console.Clear();
                                Console.WriteLine("Berta T");
                                Console.WriteLine();
                                Console.WriteLine("2 - Filtrado SACTA");
                                Console.WriteLine();
                                Console.WriteLine("Directorio de cobertura a filtrar: " + path_Cob);
                                Console.WriteLine();
                                Console.WriteLine("Directorio completo del kmz de filtros SACTA: " + path_SACTA);
                                Console.WriteLine();
                                Console.WriteLine("Directorio para exportar:");
                                string path_Exp = Console.ReadLine();

                                //Exportar
                                int Control_CM_Parte2 = -1;
                                while (Control_CM_Parte2!=0)
                                {
                                    foreach (Cobertura Cob in Filtrado.A_Operar)
                                    {
                                        var doc = Cob.CrearDocumentoSharpKML();

                                        int Control = Operaciones.CrearKML_KMZ(doc, doc.Name, "Temporal", path_Exp); //Se crea un kml temporal para después crear KMZ
                                        if (Control == 0)
                                        {
                                            Control_CM_Parte2 = 0; //Finalizar bucle
                                        }
                                        else
                                        {
                                            Console.WriteLine("Directorio de destino no válido, no puede contener puntos (.)");
                                            Console.WriteLine();
                                            Console.WriteLine("Enter para continuar");
                                            Console.ReadLine();
                                        }
                                    }
                                }
                                Console.WriteLine("Exportado con exito!");
                                Console.ReadLine();
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.Message);
                                Console.WriteLine();
                                Console.WriteLine("Enter para continuar");
                                Console.ReadLine();
                            }

                        } //Try general
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                            Console.WriteLine();
                            Console.WriteLine("Enter para continuar");
                            Console.ReadLine();
                        }
                    } //Filtrado SACTA
                    else if (Control_M == 5)//Ajustes
                    {
                        int Control_A = -1;
                        try
                        {
                            while (Control_A != 0)
                            {
                                Console.Clear();
                                Console.WriteLine("Berta T");
                                Console.WriteLine();
                                Console.WriteLine("5 - Ajustes");
                                Console.WriteLine();
                                Console.WriteLine("1 - Seleccionar comando/menú");
                                Console.WriteLine("2 - Ajustar tolerancia de filtrado múltiple (evitar la desaparición de ciertas áreas pequeñas, PUEDEN APARECER RESULTADOS ERRÓNEOS)");
                                Console.WriteLine("3 - Ajustar tolerancia de filtrado simple (evitar la desaparición de ciertas áreas pequeñas, PUEDEN APARECER RESULTADOS ERRÓNEOS)");
                                Console.WriteLine();
                                Console.WriteLine("0 - Volver a menú principal");
                                Console.WriteLine();
                                Console.WriteLine();
                                Console.WriteLine("Introduzca identificador de operación (p.e. 1):");
                                Control_A = Convert.ToInt32(Console.ReadLine());
                                if (Control_A == 1) //cambiar de version comando/menu
                                {
                                    Console.Clear();
                                    Console.WriteLine("Berta T");
                                    Console.WriteLine();
                                    Console.WriteLine("5 - Ajustes: 1 - Seleccionar comando/menú");
                                    Console.WriteLine();

                                }//cambiar de version comando/menu
                                else if(Control_A == 2) //modificar tolerancia multiple
                                {
                                    try
                                    {
                                        Console.Clear();
                                        Console.WriteLine("Berta T");
                                        Console.WriteLine();
                                        Console.WriteLine("5 - Ajustes: 3 - Ajustar tolerancia de filtrado múltiple");
                                        Console.WriteLine();
                                        Console.WriteLine("Valor actual: " + epsilon);
                                        Console.WriteLine();
                                        Console.WriteLine("Actualizar valor:");
                                        epsilon = Convert.ToDouble(Console.ReadLine());

                                        Console.Clear();
                                        Console.WriteLine("Berta T");
                                        Console.WriteLine();
                                        Console.WriteLine("5 - Ajustes: 3 - Ajustar tolerancia de filtrado múltiple");
                                        Console.WriteLine();
                                        Console.WriteLine("Valor actual: " + epsilon);
                                        Console.WriteLine();
                                        Console.ReadLine();

                                        Operaciones.GuardarAjustes(CvsM, epsilon, epsilon_simple);
                                    }
                                    catch (FormatException e)
                                    {
                                        Console.WriteLine(e.Message);
                                        Console.WriteLine();
                                        Console.WriteLine("Enter para continuar");
                                        Console.ReadLine();
                                    }

                                }//modificar tolerancia multiple
                                else if (Control_A == 3) //modificar tolerancia simple
                                {
                                    try
                                    {
                                        Console.Clear();
                                        Console.WriteLine("Berta T");
                                        Console.WriteLine();
                                        Console.WriteLine("5 - Ajustes: 3 - Ajustar tolerancia de filtrado simple");
                                        Console.WriteLine();
                                        Console.WriteLine("Valor actual: " + epsilon_simple);
                                        Console.WriteLine();
                                        Console.WriteLine("Actualizar valor:");
                                        epsilon_simple = Convert.ToDouble(Console.ReadLine());

                                        Console.Clear();
                                        Console.WriteLine("Berta M");
                                        Console.WriteLine();
                                        Console.WriteLine("5 - Ajustes: 3 - Ajustar tolerancia de filtrado simple");
                                        Console.WriteLine();
                                        Console.WriteLine("Valor actual: " + epsilon_simple);
                                        Console.WriteLine();
                                        Console.ReadLine();

                                        Operaciones.GuardarAjustes(CvsM, epsilon, epsilon_simple);
                                    }
                                    catch (FormatException e)
                                    {
                                        Console.WriteLine(e.Message);
                                        Console.WriteLine();
                                        Console.WriteLine("Enter para continuar");
                                        Console.ReadLine();
                                    }
                                }//modificar tolerancia simple
                            }
                        }
                        catch (FormatException e)//Detectar errores sobre el Control_A
                        {
                            Console.WriteLine(e.Message);
                            Console.WriteLine();
                            Console.WriteLine("Enter para continuar");
                            Console.ReadLine();
                            Control_A = -1; //Sigue el buclce
                        }
                    }//Opciones del programa
                }
                    catch (FormatException e) //Detectar errores sobre el Control_M
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine();
                    Console.WriteLine("Enter para continuar");
                    Console.ReadLine();
                    Control_M = -1; //Sigue el buclce
                }
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Zip;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Precision;
using SharpKml.Engine;


namespace Berta
{
    
    class Program
    {
        static void Main(string[] args2)
        {
            Boolean A = false;

            string DirectoryplusArgs = Environment.CommandLine;
            string onlyArgs = DirectoryplusArgs.Replace("\"" + Environment.GetCommandLineArgs()[0] + "\"", "");
            string[] args = onlyArgs.Split(",");

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

            //Queue<string> ColaComandos = new Queue<string>(); //Para guardar los distintos comandos dentro de una cola y ejecutarse uno tras de otro.

            int Control_M = -1;
            //List<string> comando = new List<string>();
            while ((Control_M != 0)&&(!A)) //Menú principal
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

                    //if(args.Length == 0) //No pasamos nada des de consola superior
                    //{
                    if (CvsM == 0) //Modo menú
                        Control_M = Convert.ToInt32(Console.ReadLine()); //Actualizar valor Control_M
                    else//Modo comando
                    {
                        Console.Clear();
                        Console.WriteLine("Berta T - COMMAND");
                        Console.WriteLine();
                        Console.WriteLine("1 - Cálculo de multi-coberturas (1,FL,DirectorioIn,Umbral(NM),NombreSalida,DirectorioOut)");
                        Console.WriteLine("2 - Filtrado SACTA (2,DirectorioCoberturasIn,DirectorioFiltro,DirectorioCOberturasOut)");
                        Console.WriteLine("3 - Cálculo de cobertura mínima ()");
                        //Console.WriteLine("4 - Crear cola de comandos");
                        Console.WriteLine("5 - Ajustes (mismo funcionamento que la visualización en menú)");
                        Console.WriteLine();
                        Console.WriteLine();
                        Console.WriteLine("0 - Finalizar");
                        Console.WriteLine();
                        Console.WriteLine();
                        Console.WriteLine("Introduzca cadena de comandos");

                        if (args2.Count() == 0) //No pasamos comando desde consola superior
                        {
                            string lin = Console.ReadLine();
                            if (lin != "0") //No cerramos aplicación
                            {
                                args = lin.Split(',');
                                Console.WriteLine(args);
                                Control_M = Convert.ToInt32(args[0]);
                            }
                            else
                                Control_M = 0; //Cerramos aplicación
                        }
                        else
                        {
                            A = true;
                            Control_M = Convert.ToInt32(args[0]);
                        }
                            
                    }

                    ////////////////// PROGRAMA ///////////////////////
                    //Ifs de control
                    if ((Control_M == 1)) //Cáclulo de multicobertura
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

                        //Parte1 - Cargar ficheros de entrada y ejecutar cálculos

                        DirectoryInfo DI = null;
                        string FL_IN = "Error";
                        bool CommandControl = true; //Control para la versión comando.
                        if (CvsM == 0) //Modo menú
                        {
                            //1.1 - FL
                            FL_IN = Operaciones.Menu_FL();

                            //1.2 - Entrada
                            (DI, Directorio_IN) = Operaciones.Menu_DirectorioIN(FL_IN,1);

                            //1.2.2 Entrada umbral de filtro (1NM de forma predeterminada)
                            Umbral_Areas = Operaciones.Menu_Umbral(Trans);

                        }//Modo menú
                        else//Modo comando
                        {
                            if(args.Length==6)
                            {
                                //1.1 - FL
                                FL_IN = Operaciones.Comando_FL(args);

                                //1.2 - Entrada
                                (DI, Directorio_IN) = Operaciones.Comando_DirectorioIN(args);

                                //1.2.2 Entrada umbral de filtro (1NM de forma predeterminada)
                                Umbral_Areas = Operaciones.Comando_Umbral(Trans, args);

                                if ((FL_IN == null) || (DI == null) || (Umbral_Areas == -1))
                                    CommandControl = false;
                                    
                            }
                            else //Comando mal
                            {
                                Console.WriteLine();
                                Console.WriteLine("El comando no cumple las caracterisitcas necesarias para ejecutar esta orden");
                                Operaciones.EscribirOutput(args, "Comando no válido, insuficientes argumentos");
                                System.Threading.Thread.Sleep(2000);
                            }

                        }//Modo comando

                        //1.3 - Cargar archivos
                            
                        Console.WriteLine("Archivos cargados:");
                        if(CommandControl==true) //si estamos en modo control y alguno de los elementos experimenta algun error no se sigue con el cálculo
                        {
                            if (DI.GetFiles().Count() > 1) //Si hay mas de 1 archivo dentro de la carpeta se ejecuta el programa
                            {
                                //Cargar coberturas originales 
                                List<Cobertura> Originales = new List<Cobertura>();
                                (Originales, NombresCargados) = Operaciones.CargarCoberturas(DI, FL_IN, args, CvsM);

                                if(Originales!=null) //Controlar excepción asociada al cargado de cobertura
                                {
                                    //1.4 - Cálculos
                                    //Originales (crear carpeta)
                                    Conjunto conjunto = new Conjunto(Originales, "original", FL_IN);

                                    //Flitrar permutaciones 
                                    Console.WriteLine();
                                    Console.WriteLine("Inicio del cálculo...");
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
                                    double segundos = Math.Round(((Math.Round(tiempo, 2) - Math.Round(tiempo, 0)) * 60) + 25, 0);
                                    if (segundos < 0)
                                        segundos = 0;
                                    Console.WriteLine("Se espera que el programa termine en unos " + Math.Round(tiempo, 0) + " minutos " + segundos + " segundos (" + DateTime.Now.ToString() + ")");
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
                                    if (Anillos != null)
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
                                        if (Anillos != null)
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
                                else if(CvsM == 0)
                                {
                                    Console.WriteLine("No se ha completado el cálculo");
                                    Console.ReadLine();
                                }

                                
                            }
                            else
                            {
                                Console.WriteLine();
                                Console.WriteLine("Esta carpeta no contiene suficientes archivos para realizar los cálculos");
                                Operaciones.EscribirOutput(args, "Error en directorio de entrada, no hay suficientes elementos");
                                if(CvsM==0)
                                    Console.ReadLine();
                            }
                        }

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
                            string NombreProyecto = "";
                            if (CvsM == 0) //Menú
                            {
                                Console.WriteLine("Introduzca nombre del proyecto (si no introduce ninguno se creara uno por defecto):");
                                NombreProyecto = Console.ReadLine();
                                if (NombreProyecto == "") //Nombre por defecto
                                {
                                    NombreProyecto = NombrePredeterminado;
                                }
                            }
                            else //Comando
                            {
                                if (args[4] == "-") //Nombre por defecto
                                {
                                    NombreProyecto = NombrePredeterminado;
                                }
                                else
                                    NombreProyecto = args[4];
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
                                string Directorio_OUT = "";
                                if (CvsM == 0)
                                    Directorio_OUT = Operaciones.Menu_DirectorioOUT(Directorio_IN, Umbral_Areas, Trans, NombresCargados, Segs, TiempoEjecución_Parte2, NombreProyecto);
                                else
                                    Directorio_OUT = Operaciones.Comando_DirectorioOUT(args[5]);

                                //2.4 - Exportar proyecto
                                if(Directorio_OUT!=null) //Control (sobretodo en versión comando)
                                {
                                    int Control = Operaciones.CrearKML_KMZ(Doc, NombreProyecto, "Temporal", Directorio_OUT); //Se crea un kml temporal para después crear KMZ
                                    if (Control == 0)
                                    {
                                        Console.WriteLine("Exportado con exito!");
                                        Console.WriteLine();
                                        Console.WriteLine("Nombre del archivo: " + NombreProyecto + ".kmz");
                                        if (CvsM == 0)
                                        {
                                            Console.WriteLine("Enter para continuar");
                                            Console.ReadLine();
                                        }
                                        Control_CM_Parte2 = 0; //Finalizar bucle
                                        Operaciones.EscribirOutput(args, "CORRECTO");
                                    }
                                    else if (CvsM == 0)
                                    {
                                        Console.WriteLine("Directorio de destino no válido, no puede contener puntos (.)");
                                        Console.WriteLine();
                                        Console.WriteLine("Enter para continuar");
                                        Console.ReadLine();
                                    }
                                }
                                else //Si se produce un error en el comando con el directorio de salida, se guardará el resultado en la carpeta temporal
                                {
                                    int Control = Operaciones.CrearKML_KMZ(Doc, NombreProyecto, "Temporal", @"Temporal"); //Se crea un kml temporal para después crear KMZ
                                    if (Control == 0)
                                    {
                                        Console.WriteLine("Exportado con exito en la carpeta Temporal (Berta Tools VX.X/Temporal)!");
                                        Console.WriteLine();
                                        Console.WriteLine("Nombre del archivo: " + NombreProyecto + ".kmz");
                                        if (CvsM == 0)
                                        {
                                            Console.WriteLine("Enter para continuar");
                                            Console.ReadLine();
                                        }
                                        else
                                        {
                                            System.Threading.Thread.Sleep(2000);
                                            Operaciones.EscribirOutput(args, "Exportado en carpeta @Temporal");
                                        }
                                        Control_CM_Parte2 = 0; //Finalizar bucle
                                    }
                                    else //A este else nunca debería entrar, por si acaso lo dejo
                                    {
                                        Console.WriteLine("Directorio de destino no válido, no puede contener puntos (.)");
                                        Console.WriteLine();
                                        if (CvsM == 0)
                                        {
                                            Console.WriteLine("Enter para continuar");
                                            Console.ReadLine();
                                        }
                                        else
                                        {
                                            System.Threading.Thread.Sleep(2000);
                                            Operaciones.EscribirOutput(args, "Error en directorio de salida (BAD)");
                                        }
                                            
                                    }
                                }
                            }
                        } //Parte2 - Guardar fichero en carpeta salida, obtener nombre de proyecto

                        args = new string[0];

                    }//Cáclulo de multicobertura
                    else if (Control_M == 2) //Filtrado SACTA
                    {
                        Console.Clear();
                        Console.WriteLine("Berta T");
                        Console.WriteLine();
                        Console.WriteLine("2 - Filtrado SACTA");

                        DirectoryInfo Directorio_Cobertura;
                        string path_Cob;

                        if((args.Count() ==4)||(CvsM==0))
                        {
                            //1 obtener directorio de entrada
                            if (CvsM == 0)
                                (Directorio_Cobertura, path_Cob) = Operaciones.Menu_DirectorioIN_SACTA();
                            else //formato comando
                                (Directorio_Cobertura, path_Cob) = Operaciones.Comando_DirectorioIN(args);

                            //Si solo hay un solo archivo en la carpeta se cargará ese, sinó se le dara al usuarió la oportunidad de elegir cual.
                            //También puede seleccionar la opción de ejecutar el filtro sobre todos los archivos de la carpeta

                            if (Directorio_Cobertura != null) //Control para la visualización comando
                            {
                                List<Cobertura> Coberturas = new List<Cobertura>();
                                List<string> NombresCargados = new List<string>();

                                //Cargar todos los archivos de la carpeta
                                (Coberturas, NombresCargados) = Operaciones.CargarCoberturas(Directorio_Cobertura, null, args,CvsM);

                                if(Coberturas != null) //Controlar excepción error de traducción
                                {
                                    //Actualizar nombres en coberturas (FL)
                                    int i = 0;
                                    foreach (Cobertura cob in Coberturas)
                                    {
                                        try
                                        {
                                            cob.FL = NombresCargados[i].Split('-')[1];
                                        }
                                        catch
                                        {
                                            try
                                            {
                                                cob.FL = NombresCargados[i].Split('_').Last();
                                            }
                                            catch
                                            {
                                                Console.WriteLine("Error en formato de entrada de los archivos, el separador del FL debería ser '-' o '_' (NOMBRE-FLXXX o NOMBRE_FLXXX)");
                                                Console.WriteLine("Error en: " + NombresCargados[i]);
                                                Console.WriteLine();
                                                Console.WriteLine("Enter para continuar");
                                                Console.ReadLine();
                                            }
                                        }
                                        i++;
                                    }

                                    if (Directorio_Cobertura.GetFiles().Count() >= 1) //Minimo un archivo
                                    {
                                        bool Control_DC = false; //Bucle para cargar
                                        while (Control_DC == false)
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
                                            int Control_DC_n = 0;
                                            if (CvsM == 0)
                                                Control_DC_n = Convert.ToInt32(Console.ReadLine());

                                            if (Control_DC_n == 0) //Ejecutar toda la carpeta, si estamos en visualización de comando también se ejecuta toda la carpeta
                                            {
                                                Control_DC = true; //terminamos bucle
                                            }
                                            else if (Control_DC_n == -1) //Cambiamos el directorio
                                            {
                                                Console.WriteLine();
                                                Console.WriteLine("Directorio de cobertura a filtrar:");
                                                //path_Cob = Console.ReadLine();
                                                //Directorio_Cobertura = new DirectoryInfo(path_Cob);
                                                (Directorio_Cobertura, path_Cob) = Operaciones.Menu_DirectorioIN_SACTA();
                                                (Coberturas, NombresCargados) = Operaciones.CargarCoberturas(Directorio_Cobertura, null, args, CvsM); //Cargamos coberturas de los archivos de esa carpeta
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
                                                Console.WriteLine("Enter para continuar");
                                                Console.ReadLine();
                                                //Seguimos el bucle
                                            }
                                        }
                                    }

                                    Conjunto conjuntoAfiltrar = new Conjunto(Coberturas, "original", "FL999");

                                    Cobertura Filtro_SACTA = null; string path_SACTA = null;

                                    //Cargar filtro SACTA
                                    (Filtro_SACTA, path_SACTA) = Operaciones.Menu_DirectorioSACTA_SACTA(NombresCargados, path_Cob, CvsM, args);

                                    if (Filtro_SACTA != null)
                                    {
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

                                        int C = Operaciones.Menu_DirectorioOUT_SACTA(path_Cob, path_SACTA, Filtrado, CvsM, args);
                                        if (C == 0)
                                        {
                                            Console.WriteLine("Exportado con exito!");

                                            if (CvsM == 0)
                                            {
                                                Console.WriteLine("Enter para continuar");
                                                Console.ReadLine();
                                            }
                                            else
                                            {
                                                Operaciones.EscribirOutput(args, "CORRECTO");
                                                System.Threading.Thread.Sleep(2000);
                                            }
                                        }
                                    }

                                }
                                else if(CvsM ==0)
                                {
                                    Console.WriteLine("No se ha completado el cálculo");
                                    Console.ReadLine();
                                }
                            }
                        }
                        else 
                        {
                            Console.WriteLine();
                            Console.WriteLine("El comando no cumple las caracterisitcas necesarias para ejecutar esta orden");
                            Operaciones.EscribirOutput(args, "Comando no válido, insuficientes argumentos");
                            System.Threading.Thread.Sleep(2000);
                        }
                        
                        

                    } //Filtrado SACTA
                    else if (Control_M == 3) //Cobertura mínima
                    {
                        //Operaciones.CargarCoberturas_CoberturaMinima(args, CvsM);

                        //List<Conjunto> CoberturasPorFL = new List<Conjunto>(); //Return, conjunto de cada FL para así ejecutar las agrupaciones de forma simple
                        var gff = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(); //Factoria para crear todos los multipoligonos o poligonos del codigo
                        List<Conjunto> AnillosFL = new List<Conjunto>();

                        SharpKml.Dom.Folder KML_Anillos = new SharpKml.Dom.Folder(); //Donde se guardarán la distirbución por anillos de FL
                        SharpKml.Dom.Folder KML_Radares = new SharpKml.Dom.Folder(); //Donde se guardarán la distirbución por radares
 
                        DirectoryInfo DI = null;
                        string directorio = null;
                        if (CvsM == 0) //modo menú
                            (DI, directorio) = Operaciones.Menu_DirectorioIN("FL999", 3);
                        else //modo comando
                            (DI, directorio) = Operaciones.Comando_DirectorioIN(args);

                        if (DI != null)
                        {
                            //Ahora ya tenemos directorio DI con carpetas, abrimos cada una de las carpetas y guardamos las coberturas
                            List<Cobertura> ListadoCobrturasPorCarpeta = new List<Cobertura>(); //Lista donde guardar TODAS las coberturas.
                            List<string> Nombres_Radar = new List<string>(); //Para crear las carpetas en mostrado por radar
                            if(DI.GetDirectories().Count()!=0)
                            {
                                foreach (DirectoryInfo carpeta in DI.GetDirectories())
                                {
                                    (List<Cobertura> L, List<string> n) = Operaciones.CargarCoberturas(carpeta, null, args, CvsM);
                                    if (L != null)
                                    {
                                        ListadoCobrturasPorCarpeta.AddRange(L);
                                        Nombres_Radar.AddRange(n);
                                    }
                                    else
                                    {
                                        DI = null;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                (ListadoCobrturasPorCarpeta, Nombres_Radar) = Operaciones.CargarCoberturas(DI, null, args, CvsM);
                                //ListadoCobrturasPorCarpeta = L;
                            }


                            //Una vez cargadas todas las coberturas, agrupamos por FL. 
                            ListadoCobrturasPorCarpeta.OrderBy(x => x.FL);
                            int FL = Convert.ToInt32(Regex.Match(ListadoCobrturasPorCarpeta[0].FL, "(\\d+)").Value); int skips = 0;
                            int FL_ini = FL;
                            int interval = Convert.ToInt32(Regex.Match(ListadoCobrturasPorCarpeta[2].FL, "(\\d+)").Value) - Convert.ToInt32(Regex.Match(ListadoCobrturasPorCarpeta[1].FL, "(\\d+)").Value);
                            Geometry Trama = null; //Coberturas que serán restadas al nivel superior
                            int FL_MAX = Convert.ToInt32(Regex.Match(ListadoCobrturasPorCarpeta.Last().FL, "(\\d+)").Value);
                            while ((FL <= FL_MAX) && (ListadoCobrturasPorCarpeta.Count > 0)) //Minetras no lleguemos a FL260 ni la lista de coberturas se quede vacía
                            {
                                SharpKml.Dom.Folder KML = new SharpKml.Dom.Folder();

                                //Obtener el FL como FLXXX
                                string FLXXX = "";
                                if (FL < 10)
                                    FLXXX = "FL00" + FL;
                                else if (FL < 100)
                                    FLXXX = "FL0" + FL;
                                else
                                    FLXXX = "FL" + FL;
                                //FLXXX = "FL010";
                                List<Cobertura> Seleccionadas = ListadoCobrturasPorCarpeta.Where(x => x.FL == FLXXX).ToList();
                                ListadoCobrturasPorCarpeta.RemoveAll(x => x.FL == FLXXX); //Eliminamos elementos con FLXXX

                                if(Seleccionadas.Count!=0) //Solo si existen coberturas se ejecuta el proceso.
                                {
                                    Conjunto Conjunto_Seleccionado = new Conjunto(Seleccionadas, "", FLXXX);

                                    Conjunto_Seleccionado.FiltrarCombinaciones();
                                    (List<Conjunto> ConjuntosPorLvl_F, Conjunto Anillos, Cobertura CoberturaMaxima) = Conjunto_Seleccionado.FormarCoberturasMultiples(epsilon, Umbral_Areas); //Calculamos multicobertura
                                    (Conjunto Resultado_Seleccionados, Cobertura NoUsado1, Cobertura NoUsado2)=Conjunto_Seleccionado.FormarCoberturasSimples(Anillos, CoberturaMaxima, epsilon_simple, Umbral_Areas);

                                    //Conjunto NuevaFinal = new Conjunto();

                                    foreach (Conjunto con in ConjuntosPorLvl_F)
                                    {
                                        Resultado_Seleccionados.A_Operar.AddRange(con.A_Operar);
                                    }

                                    Resultado_Seleccionados.FL = FLXXX;
                                    Resultado_Seleccionados.Identificador = "ColorEscala";

                                    if (Trama == null)
                                    {
                                        //Trama = Operaciones.ReducirPrecision(CoberturaTrama);
                                        Trama = Operaciones.ReducirPrecision(Conjunto_Seleccionado.FormarCoberturaTotal().Area_Operaciones);
                                    }
                                    else
                                    {
                                        foreach (Cobertura cob in Resultado_Seleccionados.A_Operar)
                                        {
                                            var Cob_Area = Operaciones.ReducirPrecision(cob.Area_Operaciones, 1000);
                                            var NewGeo = Operaciones.ReducirPrecision(Cob_Area.Difference(Trama), 1000);

                                            //Sección de código para eliminar geometrias sospechosas, vacías
                                            if (NewGeo.Area < epsilon) //Eliminar geometrias sospechosas
                                            {
                                                NewGeo = gff.CreateEmpty(Dimension.Curve);
                                            }
                                            else if (NewGeo.GetType().ToString() != "NetTopologySuite.Geometries.Polygon")
                                            {
                                                MultiPolygon Verificar = (MultiPolygon)NewGeo;
                                                var Poligonos = Verificar.Geometries;
                                                List<Polygon> Verificados = new List<Polygon>();
                                                foreach (Polygon Poly in Poligonos)
                                                {
                                                    if (Poly.Area > epsilon)
                                                    {
                                                        Verificados.Add(Poly);
                                                    }
                                                }
                                                Verificados.RemoveAll(x => x.IsEmpty == true);

                                                NewGeo = gff.CreateMultiPolygon(Verificados.ToArray());
                                            }

                                            cob.ActualizarAreas(NewGeo); //Actualizar
                                        }
                                        Trama = Trama.Union(Operaciones.ReducirPrecision(Conjunto_Seleccionado.FormarCoberturaTotal().Area_Operaciones, 100000));
                                        Resultado_Seleccionados.A_Operar.RemoveAll(x => x.Area_Operaciones.IsEmpty == true);//Eliminamos coberturas vacias
                                    }

                                    AnillosFL.Add(Resultado_Seleccionados);

                                    //SharpKml.Dom.Document KML_D = new SharpKml.Dom.Document(); //TESTING

                                    foreach (Cobertura cob in Resultado_Seleccionados.A_Operar)
                                    {
                                        KML.AddFeature(cob.CrearDocumentoSharpKML());
                                        //KML_D.AddFeature(cob.CrearDocumentoSharpKML()); //TESTING
                                    }

                                    KML_Anillos.AddFeature(KML);

                                    //Operaciones.CrearKML_KMZ(KML_D, FLXXX, "Temporal", @"Temporal"); //TESTING
                                }

                                Console.WriteLine(FL);
                                FL=FL+interval;
                            }

                            KML_Radares = Operaciones.CarpetaRedundados_CM(Nombres_Radar, AnillosFL, FL_ini, FL_MAX, interval); //CARPETAS DE ESCENARIO POR RADAR!

                            //FINAL 1 - DIRECTORIO SALIDA y NOMBRE PROYECTO

                            string Directorio_salida = ""; //Directorio de salida
                            string Nombre_Proyecto = "Cobertura mínima";
                            if(CvsM == 0) //Modo menú
                            {
                                Console.WriteLine("Introduzca nombre del proyecto (si no introduce ninguno se creara uno por defecto):");
                                Nombre_Proyecto = Console.ReadLine();
                                if (Nombre_Proyecto == "") //Nombre por defecto
                                {
                                    Nombre_Proyecto = "Cobertura mínima";
                                }

                                Directorio_salida = Operaciones.Menu_DirectorioOUT();
                            }
                            else //Modo comando
                            {
                                if (args[4] == "-") //Nombre por defecto //REVISAR INDICE YA QUE ME LO HE INVENTADO
                                {
                                    Nombre_Proyecto = "Cobertura mínima";
                                }
                                else
                                    Nombre_Proyecto = args[4]; //REVISAR INDICE YA QUE ME LO HE INVENTADO

                                Directorio_salida = Operaciones.Comando_DirectorioOUT(args[2]); //REVISAR INDICE YA QUE ME LO HE INVENTADO
                            }

                            //FINAL 2 - CREAMOS DOCUMENTO KMZ

                            Operaciones.CrearKML_KMZ(new List<SharpKml.Dom.Folder>() { KML_Anillos, KML_Radares }, Nombre_Proyecto, "Temporal", Directorio_salida);
                        }//Cobertura mínima

                        //string NombrePredeterminado = "Cobertura Mínima"; //String para guardar un nombre de proyecto predeterminado
                        //string Directorio_IN = ""; //Directorio de entrada
                        //List<string> NombresCargados = new List<string>(); //Lista donde se guardan los nombres de los archivos cargados.
                        //DirectoryInfo DI = null;

                        //if (CvsM == 0)
                        //{
                        //    (DI, Directorio_IN) = Operaciones.Menu_DirectorioIN("FL999");
                        //}
                        //else if (args.Count() == 3)
                        //{
                        //    (DI, Directorio_IN) = Operaciones.Comando_DirectorioIN(args);
                        //}

                        //if (Directorio_IN != null)
                        //{
                        //    if (DI.GetFiles().Count() > 1) //Si hay mas de 1 archivo dentro de la carpeta se ejecuta el programa
                        //    {
                        //        //Cargar coberturas originales
                        //        List<Cobertura> Originales = new List<Cobertura>();
                        //        (Originales, NombresCargados) = Operaciones.CargarCoberturas(DI, "FL999");
                        //        Conjunto conjunto = new Conjunto(Originales, "CoberturaMinima", "FL999");
                        //        conjunto.ActualizarFL(NombresCargados);
                        //        conjunto.CalcularCoberturaMinima();
                        //    }
                        //}



                    }
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
                                Console.WriteLine("4 - Generador escenarios cobertura mínima");
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
                                    Console.WriteLine("0 = Menú 1 = Comandos");
                                    int Resultado = Convert.ToInt32(Console.ReadLine());
                                    if (Resultado <= 1)
                                    {
                                        CvsM = Resultado;
                                        Operaciones.GuardarAjustes(CvsM, epsilon, epsilon_simple);
                                        Console.WriteLine();
                                        Console.WriteLine("Formato de control actualizado.");
                                        Console.ReadLine();
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error");
                                        Console.ReadLine();
                                    }

                                }//cambiar de version comando/menu
                                else if (Control_A == 2) //modificar tolerancia multiple
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
                                else if (Control_A == 4) //Preparador de ejercicios de Cobertura Mínima
                                {
                                    DirectoryInfo DI = null;
                                    string directorio = null;
                                    int intervalo = 0;
                                    string directorio_out = null;
                                    string FL_init = "";
                                    string FL_fin = "";

                                    if (CvsM == 0) //modo menú
                                    {
                                        (DI, directorio) = Operaciones.Menu_DirectorioIN("FL999", 3);
                                        Console.WriteLine("Introducir FL de inicio");
                                        Console.ReadLine();
                                        FL_init = Operaciones.Menu_FL();
                                        Console.WriteLine("Introducir FL finalr");
                                        Console.ReadLine();
                                        FL_fin = Operaciones.Menu_FL();
                                        Console.WriteLine("Introducir a continuación intervalo FL (FLXXX), enter para continuar");
                                        Console.ReadLine();
                                        string FL_Interval = Operaciones.Menu_FL();
                                        Match m = Regex.Match(FL_Interval, "(\\d+)");
                                        intervalo = Convert.ToInt32(m.Value);
                                        while(directorio_out==null)
                                        {
                                            Console.WriteLine("Introducir directorio para guardar escenario");
                                            directorio_out = Operaciones.Comando_DirectorioOUT(Console.ReadLine());
                                        }
                                        
                                    }
                                        
                                    else //modo comando
                                    {
                                        (DI, directorio) = Operaciones.Comando_DirectorioIN(args);
                                        string FL_Interval = Operaciones.Comando_FL(args);
                                        Match m = Regex.Match(FL_Interval, "(\\d+)");
                                        intervalo = Convert.ToInt32(string.Empty);
                                        directorio_out = Operaciones.Comando_DirectorioOUT(Console.ReadLine());
                                    }
                                        
                                    if (DI != null)
                                    {
                                        //Ahora ya tenemos directorio DI con carpetas, abrimos cada una de las carpetas y guardamos las coberturas
                                        if (DI.GetDirectories().Count() != 0)
                                        {
                                            foreach (DirectoryInfo carpeta in DI.GetDirectories())
                                            {
                                                int FL = Convert.ToInt32(Regex.Match(FL_init, "(\\d+)").Value);
                                                while(FL <= Convert.ToInt32(Regex.Match(FL_fin, "(\\d+)").Value))
                                                {
                                                    string FLXXX = "";
                                                    if (FL < 10)
                                                        FLXXX = "FL00" + FL;
                                                    else if (FL < 100)
                                                        FLXXX = "FL0" + FL;
                                                    else
                                                        FLXXX = "FL" + FL;
                                                    try
                                                    {
                                                        //Seleccionamos archivo de cada intervalo
                                                        FileInfo Cobertura_Escenario = carpeta.GetFiles().Where(x => (x.Name.Split("_").Last().Split(".").First() == FLXXX) || (x.Name.Split("-").Last().Split(".").First() == FLXXX)).ToList()[0];
                                                        Cobertura_Escenario.CopyTo(Path.Combine(directorio_out, Cobertura_Escenario.Name)); //Copiamos a carpeta elegida para guardar escenario
                                                    }
                                                    catch
                                                    {
                                                        //---
                                                    }
                                                    

                                                    FL = FL + intervalo;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            //Lo mismo pero para el caso de una carpeta llena de archivos (no una carpeta con directorios)
                                            int FL = Convert.ToInt32(Regex.Match(FL_init, "(\\d+)").Value);
                                            while (FL <= Convert.ToInt32(Regex.Match(FL_fin, "(\\d+)").Value))
                                            {
                                                string FLXXX = "";
                                                if (FL < 10)
                                                    FLXXX = "FL00" + FL;
                                                else if (FL < 100)
                                                    FLXXX = "FL0" + FL;
                                                else
                                                    FLXXX = "FL" + FL;

                                                try 
                                                {
                                                    List<FileInfo> Listado_Coberturas_Escenario = DI.GetFiles().Where(x => (x.Name.Split("_").Last().Split(".").First() == FLXXX) || (x.Name.Split("-").Last().Split(".").First() == FLXXX)).ToList();
                                                    Listado_Coberturas_Escenario.ForEach(x => x.CopyTo(Path.Combine(directorio_out, x.Name))); //Soy buenisimo omg. Ejecuta la misma orden para todos. 
                                                }
                                                catch
                                                {
                                                    //---
                                                }

                                                FL = FL + intervalo;
                                            }
                                        }
                                    }
                                } //Preparador de ejercicios Cobertura Mínima
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
                    if (CvsM == 0)
                    {
                        Console.WriteLine("Enter para continuar");
                        Console.ReadLine();
                        Control_M = -1; //Sigue el buclce
                    }
                    else if(A)
                    {
                        Control_M = 0;
                    }
                    else
                        Control_M = -1; //Sigue el buclce


                }
            }
        }
    }
}

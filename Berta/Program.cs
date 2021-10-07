using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using SharpKml.Engine;


namespace Berta
{
    class Program
    {
        public static class Operaciones
        {
            public static int CrearKML_KMZ(SharpKml.Dom.Document Doc, string NombreDoc, string carpeta, string Destino)
            {
                string path = Path.Combine(Path.Combine(@".\" + carpeta + "", NombreDoc + "_ALL.kml"));
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

        }

        public class Conjunto
        {
            public List<Cobertura> A_Operar = new List<Cobertura>(); //Lista de coberturas
            public string Identificador; //Identificador (opcional)
            public string FL;
            public string Nombre_Resultado; //Nombre del resultado (dependiendo de la ultima operación ejecutada)
            ICollection<Geometry> Areas = new List<Geometry>(); //Areas de coberturas (privado)
            List<string> Nombres = new List<string>();
            public Conjunto()
            { }

            public Conjunto(List<Cobertura> Coberturas, string ID, string Fl)
            {
                A_Operar = Coberturas;
                Identificador = ID;
                FL = Fl;
                int i = 1;
                string N = Coberturas[0].nombre.Split('-')[0];
                List<string> NN = new List<string>();
                NN.Add(N);
                Areas.Add(Coberturas[0].Area_Operaciones);

                while (i<Coberturas.Count())
                {
                    Areas.Add(Coberturas[i].Area_Operaciones);
                    NN.Add(Coberturas[i].nombre);
                    N = N + " () " + Coberturas[i].nombre.Split('-')[0];
                    i++;
                }
                Nombre_Resultado = N;
                Nombres = NN;
            }// Extrae de las coberturas entradas las areas para poder trabajar con ellas, genera nombre (inicalmente en null)

            private IEnumerable<IEnumerable<T>> Permutaciones<T>(IEnumerable<T> items, int count)
            {
                int i = 0;
                foreach (var item in items)
                {
                    if (count == 1)
                        yield return new T[] { item };
                    else
                    {
                        foreach (var result in Permutaciones(items.Skip(i + 1), count - 1))
                            yield return new T[] { item }.Concat(result);
                    }

                    ++i;
                }
            }//Permutar nombres

            //Metodos geometricos
            public Geometry Union()
            {
                CascadedPolygonUnion ExecutarUnion = new CascadedPolygonUnion(Areas);
                var GEO = ExecutarUnion.Union(); //union ejecutada

                return GEO;
            }//ejecuta la union de todos las coberuras inscritas

            public Geometry Intersección_Todos()
            {
                List<Geometry> N_Areas = Areas.ToList();
                var GEO = N_Areas[0].Intersection(N_Areas[1]);

                if(N_Areas.Count>2)
                {
                    int i = 2;
                    while(i< N_Areas.Count)
                    {
                        GEO = GEO.Intersection(N_Areas[i]);
                        i++;
                    }
                }

                return GEO;
            } //Ejecuta la interseccion de todas las coberutras (obtener multicobertura mayor)

            //Metodos tipologicos

            public List<Cobertura> RetornarCoberturasOriginales()
            {
                return A_Operar;
            }

            public Cobertura FormarCoberturaTotal()
            {
                if (Areas.Count ==0)
                {
                    foreach(Cobertura c in A_Operar)
                        Areas.Add(c.Area_Operaciones);
                }

                CascadedPolygonUnion ExecutarUnion = new CascadedPolygonUnion(Areas);
                var GEO = ExecutarUnion.Union(); //geometria a retornar 

                return new Cobertura(Nombre_Resultado, this.FL, "total", GEO);
            } //Union de todas las coberturas

            private List<Conjunto> FormarCoberturasMultiples_Paso1() //Conjuntos por lvl
            {
                //Generamos todas las multicoberturas des de lvl Max a 2
                int MultipleMax = A_Operar.Count; //El número de radares determina la cobertura múltiple máxima possible

                List<Conjunto> Conjuntos = new List<Conjunto>(); //Guardaremos los conjuntos (por nivel) de multicoberturas

                while(MultipleMax>1)
                {
                    var Multi = Permutaciones(Nombres, MultipleMax); //Generar permutaciones de nombres para así conocer las combinaciones necesarias
                    List<Cobertura> Coberturas = new List<Cobertura>(); //Lista de resultados

                    foreach (var Permuta in Multi)
                    {
                        List<Geometry> Operables = new List<Geometry>();//Lista para guardar las coberturas indicadas por la permutación

                        foreach (var Radar in Permuta) //Buscamos cada cobertura 
                        {
                            Operables.Add(A_Operar.Where(x => x.nombre.Split('-')[0] == Radar).ToList()[0].Area_Operaciones);
                        }
                        string N_Completo = String.Join(" () ", Permuta); //Nombre completo de la combianción
                        var BASE = Operables.First(); //Elegimos una cobertura que será nuestra base para calcular la intersección

                        int i = 1;
                        while (i < Operables.Count) //Ejecutamos intersección de la combinación
                        {
                            BASE = BASE.Intersection(Operables[i]);
                            i++;
                        }

                        if (BASE.IsEmpty == false) //Si el resultado no es nulo (No interseccionan por lo que el resultado es nulo) creamos nueva cobertura y añadimos al conjunto
                        {
                            Coberturas.Add(new Cobertura(N_Completo, this.FL, "multi", MultipleMax, BASE));
                        }

                    }
                    if(Coberturas.Count != 0) //Tenemos intersección, guardamos el resultado. En caso contrario no guardamos nada (si el count es 0 significa que no se ha guardado ninguna cobertura
                        Conjuntos.Add(new Conjunto(Coberturas, "multi " + MultipleMax, this.FL));                               // por lo que no existe intersección)

                    MultipleMax--;
                }

                return Conjuntos; 
            }

            private (Cobertura,bool) FormarCoberturasMultiples_Paso2(List<Conjunto> CoberturasPorLvl) //Calcular cobertura máxima
            {
                int MultipleMax = this.A_Operar.Count; //El número de radares determina la cobertura múltiple máxima possible

                Cobertura MAX = new Cobertura();
                bool CobMax = false;

                if (CoberturasPorLvl.First().A_Operar.First().tipo_multiple == MultipleMax)
                {
                    MAX = CoberturasPorLvl.First().A_Operar.First();
                    MAX.nombre = "(MAX)";
                    CobMax = true;
                }

                if (CobMax)
                    return (MAX,true);
                else
                    return (null,false);
            }

            private (Conjunto, List<Conjunto>) FormarCoberturasMultiples_Paso3(List<Conjunto> CoberturasPorLvl, bool CoberturaMAX) //Cálcula anillos de coberturas del mismo lvl
            {
                //Dos casos, hay multiple máx o no. Si la hay se ejecuta como en version Alpha
                List<Cobertura> TotalPorLVL = new List<Cobertura>(); //Lista para guardar la unión por lvl de multicobertura (anillos)

                if (CoberturaMAX) //Hay cobertura max
                {
                    var GEO_Resta = CoberturasPorLvl.First().A_Operar.First().Area_Operaciones; //Area que restaremos a todas las múlticoberturas para así mostrar correctamente el resultado

                    int j = 1; //El primero no se calcula (MAX)
                    while (j < CoberturasPorLvl.Count)
                    {
                        foreach (Cobertura Cob in CoberturasPorLvl[j].A_Operar)
                        {
                            //Restar
                            var NewGeo = Cob.Area_Operaciones.Difference(GEO_Resta);
                            Cob.ActualizarAreas(NewGeo); //Actualizar
                        }
                        CoberturasPorLvl[j].A_Operar.RemoveAll(x => x.Area_Operaciones.IsEmpty == true);

                        var UnionLvl = CoberturasPorLvl[j].FormarCoberturaTotal();
                        TotalPorLVL.Add(new Cobertura("", this.FL, "multiple total", CoberturasPorLvl[j].A_Operar[0].tipo_multiple, UnionLvl.Area_Operaciones.Difference(GEO_Resta)));
                        GEO_Resta = GEO_Resta.Union(UnionLvl.Area_Operaciones);
                        TotalPorLVL.RemoveAll(x => x.Area_Operaciones.IsEmpty == true);//Eliminamos coberturas vacias

                        j++;
                    }
                }
                else //No hay cobertura max
                {
                    //Formar Geo_Resta, para hacer la diferencia sobre las otras capas
                    Cobertura GEO_Resta = CoberturasPorLvl.First().FormarCoberturaTotal();
                    TotalPorLVL.Add(new Cobertura("", this.FL, "multiple total", CoberturasPorLvl.First().A_Operar.First().tipo_multiple, GEO_Resta.Area_Operaciones)); //Guardamos el primer anillo 

                    //Geo_Resta inicialmente es la unión de todas las coberturas de lvl máximo (que no cobertura de lvl máximo, ya que en este caso no existe)
                    //Ahora unimos cada anillo y ejecutamos la resta para obtener el resultado correcto
                    int j = 1;
                    while(j< CoberturasPorLvl.Count)
                    {
                        var NewG = CoberturasPorLvl[j].FormarCoberturaTotal(); //Ejecutamos unión de conjunto
                        var Resta = NewG.Area_Operaciones.Difference(GEO_Resta.Area_Operaciones); //Hacemos la diferencia 

                        TotalPorLVL.Add(new Cobertura("", this.FL, "multiple total", CoberturasPorLvl[j].A_Operar[0].tipo_multiple, Resta)); //Guardamos anillo resultante

                        foreach (Cobertura Cob in CoberturasPorLvl[j].A_Operar)
                        {
                            //Restar
                            var NewGeo = Cob.Area_Operaciones.Difference(GEO_Resta.Area_Operaciones);
                            if(NewGeo.Coordinates.Count()<11) //Si una geometria tiene menos de 10 puntos la consideramos como empty.
                            {
                                var gff = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();
                                NewGeo = gff.CreateEmpty(Dimension.Curve);
                            }
                            Cob.ActualizarAreas(NewGeo); //Actualizar
                        }

                        GEO_Resta.ActualizarAreas(NewG.Area_Operaciones.Union(GEO_Resta.Area_Operaciones)); //Actualizamos area de resta (GEO_resta)

                        CoberturasPorLvl[j].A_Operar.RemoveAll(x => x.Area_Operaciones.IsEmpty == true);//Eliminamos coberturas vacias
                        TotalPorLVL.RemoveAll(x => x.Area_Operaciones.IsEmpty == true); //Eliminamos coberturas vacias (si las hay)

                        j++;
                    }
                }

                Conjunto TotalPorLvL = new Conjunto(TotalPorLVL, "Total por lvl", this.FL); //Generamos un conjunto con todos los anillos

                return (TotalPorLvL, CoberturasPorLvl);
            }

            public (List<Conjunto>, Conjunto, Cobertura) FormarCoberturasMultiples()
            {
                List<Conjunto> ConjuntosPorLvl = FormarCoberturasMultiples_Paso1(); //Ejecutar paso 1 

                (Cobertura CoberturaMaxima, bool existeMax) = FormarCoberturasMultiples_Paso2(ConjuntosPorLvl); //Ejecutar paso 2

                (Conjunto Anillos, List<Conjunto> ConjuntosPorLvl_F) = FormarCoberturasMultiples_Paso3(ConjuntosPorLvl, existeMax);

                if (existeMax)
                {
                    ConjuntosPorLvl.RemoveAt(0); //Eliminamos cobertura máxima ya que esta presenter en otro parámetro
                    return (ConjuntosPorLvl_F, Anillos, CoberturaMaxima);
                }
                else
                    return (ConjuntosPorLvl_F, Anillos, null); //Retornamos null la cobertura máxima
            }

            public (Conjunto, Cobertura, Cobertura) FormarCoberturasSimples(Conjunto TotalPorLvl, Cobertura Max)
            {
                
                Cobertura InterseccionTotal = new Cobertura();
                InterseccionTotal.FL = this.FL;
                InterseccionTotal.nombre = "multiple";
                InterseccionTotal.tipo = "total";
                InterseccionTotal.tipo_multiple = 0;

                if (Max != null) //Caso con cobertura máxima existente
                {
                    //Primero unimos todas las intersecciones para asi crear la cobertura de intersección total 
                    var GEOt = Max.Area_Operaciones;
                    foreach (Cobertura cob in TotalPorLvl.A_Operar)
                        GEOt = GEOt.Union(cob.Area_Operaciones);
                    InterseccionTotal.ActualizarAreas(GEOt);
                }
                else //Caso donde cobertura máxima no existe
                {
                    //Primero unimos todas las intersecciones para asi crear la cobertura de intersección total 
                    var GEOt = TotalPorLvl.A_Operar.First().Area_Operaciones;
                    int j = 1; 
                    while(j<TotalPorLvl.A_Operar.Count)
                    {
                        GEOt = GEOt.Union(TotalPorLvl.A_Operar[j].Area_Operaciones);
                        j++;
                    }
                    InterseccionTotal.ActualizarAreas(GEOt);
                }

                //Crear simple total
                Cobertura SimplesTotal = new Cobertura("", this.FL, "simple total", FormarCoberturaTotal().Area_Operaciones.Difference(InterseccionTotal.Area_Operaciones));

                //Restar para cada original la intersección total y asi obtener las coberutras simples 
                List<Cobertura> Ret = new List<Cobertura>();

                foreach (Cobertura COB in A_Operar)
                {
                    var GEO = COB.Area_Operaciones.Difference(InterseccionTotal.Area_Operaciones);
                    Ret.Add(new Cobertura(COB.nombre, this.FL, "simple", GEO));
                }

                Conjunto Simples = new Conjunto(Ret, "Coberturas simples", this.FL);
                return (Simples, InterseccionTotal, SimplesTotal);

            } //coberturas simples (por cada radar y el conjunto de ellas) y intersección total (union de todo eso que no es múltiple)

        }

        public class Cobertura
        {
            /*
             * Formato Nombres: 
             * 1 Cobertura original: Obtenida directamente del KML
             *      nombre = Radar
             *      tipo = original
             *      
             * 2 Cobertura simple: Esa zona donde solo encontramos una cobertura
             *      nombre = Radar
             *      tipo = simple
             *      
             * 3 Cobertura simple total: Union de todas als coberturas simples
             *      nombre = "NaN"
             *      tipo = simple total
             *      
             * 4 Cobertura multiple: Esa zona donde existen mas de una cobertura
             *      nombre = Radares influyentes
             *      tipo = multiple "+tipo_multiple
             *      
             * 5 Cobertura multiple total: Union de todas las coberturas multiples del mismo nivel
             *      nombre = "NaN"
             *      tipo = multiple total "+tipo_multiple
             *      
             * 6 Cobertura total: Union de todas las coberturas
             *      nombre = "NaN"
             *      tipo = total
             * 
             * El nombre a exportar se guardara en la variable Nombre_exp, solo usada en el momento de crear el KML
             */
            public string nombre; 
            public string FL;
            public string tipo;
            public int tipo_multiple=0; //De que grado de multicobertura se trata si es que es tipo "multiple"

            public Geometry Area_Operaciones; //Cobertura-Huecos (Futura implementación)

            //public string nombre_Archivo;

            //Constructores
            public Cobertura()
            { } //Simple

            public Cobertura(string Nombre, string Fl, string Tipo, Geometry Poligono)
            {
                nombre = Nombre; ;
                FL = Fl;
                tipo = Tipo;

                Area_Operaciones = Poligono;

            } //tipo simple, original, total. CASO SIMPLE

            public Cobertura(string Nombre, string Fl, string Tipo, List<Geometry> Poligono)
            {
                nombre = Nombre; ;
                FL = Fl;
                tipo = Tipo;

                if(Poligono.Count ==1) //Si en la lista de poligonos solo hay uno lo guardamos directamente
                    Area_Operaciones = Poligono.First();
                else //Si hay más de uno ejecutamos la union
                {
                    ICollection<Geometry> Areas = new List<Geometry>(); //La clase CascadedPolygonUnion requiere de un ICollection

                    foreach (Geometry c in Poligono)
                        Areas.Add(c);

                    CascadedPolygonUnion ExecutarUnion = new CascadedPolygonUnion(Areas); //Se crea clase para ejecutar la unón
                    Area_Operaciones = ExecutarUnion.Union(); //geometria a retornar 
                }
            } //tipo simple, original, total. CASO MULTIPOLIGONO

            public Cobertura(string Nombre, string Fl, string Tipo, int Tipo_Multiple, Geometry Poligono)
            {
                nombre = Nombre; ;
                FL = Fl;
                tipo = Tipo;
                tipo_multiple = Tipo_Multiple;

                Area_Operaciones = Poligono;
            } //tipo multiple

            public Cobertura(string Nombre, string Fl, string Tipo, List<Geometry> Poligono, List<Geometry> Huecos)
            {
                nombre = Nombre; ;
                FL = Fl;
                tipo = Tipo;

                Geometry BASE;
                if (Poligono.Count == 1) //Si en la lista de poligonos solo hay uno lo guardamos directamente
                    BASE = Poligono.First();
                else //Si hay más de uno ejecutamos la union
                {
                    ICollection<Geometry> Areas = new List<Geometry>(); //La clase CascadedPolygonUnion requiere de un ICollection

                    foreach (Geometry c in Poligono)
                        Areas.Add(c);

                    CascadedPolygonUnion ExecutarUnion = new CascadedPolygonUnion(Areas); //Se crea clase para ejecutar la unón
                    BASE = ExecutarUnion.Union(); //geometria a retornar 
                }

                Geometry TRAMA;
                //Juntar todos los huecos 
                if (Huecos.Count == 1) //Si en la lista de poligonos solo hay uno lo guardamos directamente
                    TRAMA = Poligono.First();
                else //Si hay más de uno ejecutamos la union
                {
                    ICollection<Geometry> Areas = new List<Geometry>(); //La clase CascadedPolygonUnion requiere de un ICollection

                    foreach (Geometry c in Huecos)
                        Areas.Add(c);

                    CascadedPolygonUnion ExecutarUnion = new CascadedPolygonUnion(Areas); //Se crea clase para ejecutar la unón
                    TRAMA = ExecutarUnion.Union(); //geometria a retornar 
                }

                //Ejecutar diferencia para encontrar cobertura final
                Area_Operaciones = BASE.Difference(TRAMA);

            } //generar original con huecos

            //Metodos
            public void ActualizarAreas(Geometry New)
            {
                Area_Operaciones = New;
            }

            private string Nombre_exp()
            {
                string r;
                if (tipo == "total")
                    r = "Cobertura Total "+this.FL;
                else if (tipo == "multiple total")
                    r = string.Format("{0:00}", tipo_multiple) + " Total" ;
                else if (tipo == "simple total")
                    r = string.Format("{0:00}", 1) + " Total";
                else
                {
                    //Reescriptura del nombre a nuevo formato propuesto por Enaire
                    List<string> V = nombre.Split(" () ").ToList();
                    V = V.OrderBy(x => x).ToList(); //Ordenar alfabeticamente
                    nombre = string.Join('.', V);
                    r = nombre;
                }
                    

                return r;
            } //Forma el nombre en el fromato deseado por Enaire

            private SharpKml.Dom.Style Estilo()
            {
                SharpKml.Dom.Style style = new SharpKml.Dom.Style();
                SharpKml.Dom.PolygonStyle pstyle = new SharpKml.Dom.PolygonStyle();
                if ((tipo == "original")|| (tipo == "total")|| (tipo == "simple")|| (tipo == "simple total"))
                {
                    //Color poligono
                    pstyle.Color = new SharpKml.Base.Color32(125, 0, 255, 0);
                    style.Polygon = pstyle;

                    //Color borde
                    SharpKml.Dom.LineStyle lineStyle = new SharpKml.Dom.LineStyle();
                    lineStyle.Color = new SharpKml.Base.Color32(127, 255, 255, 255);
                    lineStyle.Width = 1;
                    style.Line = lineStyle;
                }
                else
                {
                    if (tipo_multiple == 2)
                    {
                        //Color poligono
                        pstyle.Color = new SharpKml.Base.Color32(153, 0, 76, 152);
                        style.Polygon = pstyle;

                        //Color borde
                        SharpKml.Dom.LineStyle lineStyle = new SharpKml.Dom.LineStyle();
                        lineStyle.Color = new SharpKml.Base.Color32(127, 255, 255, 255);
                        lineStyle.Width = 1.5;
                        style.Line = lineStyle;
                    }
                    else if (tipo_multiple == 3)
                    {
                        //Color poligono
                        pstyle.Color = new SharpKml.Base.Color32(153, 127, 0, 255);
                        style.Polygon = pstyle;

                        //Color borde
                        SharpKml.Dom.LineStyle lineStyle = new SharpKml.Dom.LineStyle();
                        lineStyle.Color = new SharpKml.Base.Color32(127, 255, 255, 255);
                        lineStyle.Width = 1.5;
                        style.Line = lineStyle;
                    }
                    else 
                    {
                        //Color poligono
                        pstyle.Color = new SharpKml.Base.Color32(178, 0, 85, 255);
                        style.Polygon = pstyle;

                        //Color borde
                        SharpKml.Dom.LineStyle lineStyle = new SharpKml.Dom.LineStyle();
                        lineStyle.Color = new SharpKml.Base.Color32(178, 255, 255, 255);
                        lineStyle.Width = 1.5;
                        style.Line = lineStyle;
                    }
                }

                return style;
            }

            public SharpKml.Dom.Document CrearDocumentoSharpKML()
            {
                List<SharpKml.Dom.Placemark> lista = new List<SharpKml.Dom.Placemark>();

                //Mirar si es un poligono o un multipoligono y obtener transformación de librerias 
                if (Area_Operaciones.GetType().ToString() == "NetTopologySuite.Geometries.Polygon")
                {
                    var Extr = ((Polygon)Area_Operaciones).ExteriorRing.Coordinates;
                    if (((Polygon)Area_Operaciones).InteriorRings.Length != 0)
                    {
                        //CASO CON HUECOS!!
                        SharpKml.Dom.Polygon polygon = new SharpKml.Dom.Polygon(); //se crea poligono
                        //Bucle inner
                        var Intr = ((Polygon)Area_Operaciones).InteriorRings;
                        foreach (LineString Forat in Intr)
                        {
                            int x = 0;
                            int Maxx = Forat.Coordinates.Length;
                            SharpKml.Dom.InnerBoundary innerBoundary = new SharpKml.Dom.InnerBoundary();
                            innerBoundary.LinearRing = new SharpKml.Dom.LinearRing();
                            innerBoundary.LinearRing.Coordinates = new SharpKml.Dom.CoordinateCollection();
                            while (x < Maxx)
                            {
                                innerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Forat.Coordinates[x].Y, Forat.Coordinates[x].X));
                                x++;
                            }
                            polygon.AddInnerBoundary(innerBoundary);
                        }

                        //Bucle outer
                        int j = 0;
                        int Max = Extr.Length;
                        SharpKml.Dom.OuterBoundary outerBoundary = new SharpKml.Dom.OuterBoundary();
                        outerBoundary.LinearRing = new SharpKml.Dom.LinearRing();
                        outerBoundary.LinearRing.Coordinates = new SharpKml.Dom.CoordinateCollection();
                        while (j < Max)
                        {
                            outerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Extr[j].Y, Extr[j].X));
                            j++;
                        }
                        polygon.OuterBoundary = outerBoundary; //se importan dentro del poligono las coordenadas SOLO DEL POLYGONO EXTERIOR!
                        SharpKml.Dom.Placemark placemark = new SharpKml.Dom.Placemark(); //se crea placemark
                        placemark.Name = Nombre_exp();
                        placemark.Geometry = polygon; //añadir geometria al placemark
                        placemark.AddStyle(Estilo());

                        lista.Add(placemark);
                    }
                    else
                    {
                        //CASO SIN HUECOS
                        SharpKml.Dom.Polygon polygon = new SharpKml.Dom.Polygon(); //se crea poligono
                        //Bucle outer
                        int j = 0;
                        int Max = Extr.Length;
                        SharpKml.Dom.OuterBoundary outerBoundary = new SharpKml.Dom.OuterBoundary();
                        outerBoundary.LinearRing = new SharpKml.Dom.LinearRing();
                        outerBoundary.LinearRing.Coordinates = new SharpKml.Dom.CoordinateCollection();
                        while (j < Max)
                        {
                            outerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Extr[j].Y, Extr[j].X));
                            j++;
                        }
                        polygon.OuterBoundary = outerBoundary; //se importan dentro del poligono las coordenadas SOLO DEL POLYGONO EXTERIOR!
                        SharpKml.Dom.Placemark placemark = new SharpKml.Dom.Placemark(); //se crea placemark
                        placemark.Name = Nombre_exp();
                        placemark.Geometry = polygon; //añadir geometria al placemark
                        placemark.AddStyle(Estilo());

                        lista.Add(placemark);
                    }
                }
                else if (Area_Operaciones.GetType().ToString() == "NetTopologySuite.Geometries.MultiPolygon")
                {
                    foreach (Geometry GEO in ((MultiPolygon)Area_Operaciones).Geometries)
                    {
                        var Extr = ((Polygon)GEO).ExteriorRing.Coordinates;
                        if (((Polygon)GEO).InteriorRings.Length != 0)
                        {
                            //CASO CON HUECOS!!
                            SharpKml.Dom.Polygon polygon = new SharpKml.Dom.Polygon(); //se crea poligono
                                                                                       //Bucle inner
                                                                                       //int con = 1;
                            var Intr = ((Polygon)GEO).InteriorRings;
                            foreach (LineString Forat in Intr)
                            {
                                int x = 0;
                                int Maxx = Forat.Coordinates.Length;
                                SharpKml.Dom.InnerBoundary innerBoundary = new SharpKml.Dom.InnerBoundary();
                                innerBoundary.LinearRing = new SharpKml.Dom.LinearRing();
                                innerBoundary.LinearRing.Coordinates = new SharpKml.Dom.CoordinateCollection();
                                while (x < Maxx)
                                {
                                    innerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Forat.Coordinates[x].Y, Forat.Coordinates[x].X));
                                    x++;
                                }
                                polygon.AddInnerBoundary(innerBoundary);
                            }

                            //Bucle outer
                            int j = 0;
                            int Max = Extr.Length;
                            SharpKml.Dom.OuterBoundary outerBoundary = new SharpKml.Dom.OuterBoundary();
                            outerBoundary.LinearRing = new SharpKml.Dom.LinearRing();
                            outerBoundary.LinearRing.Coordinates = new SharpKml.Dom.CoordinateCollection();
                            while (j < Max)
                            {
                                outerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Extr[j].Y, Extr[j].X));
                                j++;
                            }
                            polygon.OuterBoundary = outerBoundary; //se importan dentro del poligono las coordenadas SOLO DEL POLYGONO EXTERIOR!
                            SharpKml.Dom.Placemark placemark = new SharpKml.Dom.Placemark(); //se crea placemark
                            placemark.Name = Nombre_exp();
                            placemark.Geometry = polygon; //añadir geometria al placemark
                            placemark.AddStyle(Estilo());

                            lista.Add(placemark);
                        }
                        else
                        {
                            //CASO SIN HUECOS
                            SharpKml.Dom.Polygon polygon = new SharpKml.Dom.Polygon(); //se crea poligono
                                                                                       //Bucle outer
                            int j = 0;
                            int Max = Extr.Length;
                            SharpKml.Dom.OuterBoundary outerBoundary = new SharpKml.Dom.OuterBoundary();
                            outerBoundary.LinearRing = new SharpKml.Dom.LinearRing();
                            outerBoundary.LinearRing.Coordinates = new SharpKml.Dom.CoordinateCollection();
                            while (j < Max)
                            {
                                outerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Extr[j].Y, Extr[j].X));
                                j++;
                            }
                            polygon.OuterBoundary = outerBoundary; //se importan dentro del poligono las coordenadas SOLO DEL POLYGONO EXTERIOR!
                            SharpKml.Dom.Placemark placemark = new SharpKml.Dom.Placemark(); //se crea placemark
                            placemark.Name = Nombre_exp();
                            placemark.Geometry = polygon; //añadir geometria al placemark
                            placemark.AddStyle(Estilo());

                            lista.Add(placemark);
                        }
                    }
                }

                var Doc = new SharpKml.Dom.Document(); //se crea documento
                Doc.Name = Nombre_exp();
                foreach (SharpKml.Dom.Placemark placemark in lista)
                {
                    Doc.AddFeature(placemark); //añadir placermak dentro del documento
                }

                return Doc;
            } //Traduce de NetTopologySuite a SharpKML y guarda la infromación asociada en un Documento SharkKML (NO KML)

            public string CrearKML_Simple(string carpeta)
            {
                List<SharpKml.Dom.Placemark> lista = new List<SharpKml.Dom.Placemark>();

                //Mirar si es un poligono o un multipoligono y obtener transformación de librerias 
                if (Area_Operaciones.GetType().ToString() == "NetTopologySuite.Geometries.Polygon")
                {
                    var Extr = ((Polygon)Area_Operaciones).ExteriorRing.Coordinates;
                    if (((Polygon)Area_Operaciones).InteriorRings.Length != 0)
                    {
                        //CASO CON HUECOS!!
                        SharpKml.Dom.Polygon polygon = new SharpKml.Dom.Polygon(); //se crea poligono
                        //Bucle inner
                        var Intr = ((Polygon)Area_Operaciones).InteriorRings;
                        foreach(LineString Forat in Intr)
                        {
                            int x = 0;
                            int Maxx = Forat.Coordinates.Length;
                            SharpKml.Dom.InnerBoundary innerBoundary = new SharpKml.Dom.InnerBoundary();
                            innerBoundary.LinearRing = new SharpKml.Dom.LinearRing();
                            innerBoundary.LinearRing.Coordinates = new SharpKml.Dom.CoordinateCollection();
                            while (x < Maxx)
                            {
                                innerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Forat.Coordinates[x].Y, Forat.Coordinates[x].X));
                                x++;
                            }
                            polygon.AddInnerBoundary(innerBoundary);
                        }

                        //Bucle outer
                        int j = 0;
                        int Max = Extr.Length;
                        SharpKml.Dom.OuterBoundary outerBoundary = new SharpKml.Dom.OuterBoundary();
                        outerBoundary.LinearRing = new SharpKml.Dom.LinearRing();
                        outerBoundary.LinearRing.Coordinates = new SharpKml.Dom.CoordinateCollection();
                        while (j < Max)
                        {
                            outerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Extr[j].Y, Extr[j].X));
                            j++;
                        }
                        polygon.OuterBoundary = outerBoundary; //se importan dentro del poligono las coordenadas SOLO DEL POLYGONO EXTERIOR!
                        SharpKml.Dom.Placemark placemark = new SharpKml.Dom.Placemark(); //se crea placemark
                        placemark.Name = tipo; //nombre de la area
                        placemark.Geometry = polygon; //añadir geometria al placemark

                        lista.Add(placemark);
                    }
                    else
                    {
                        //CASO SIN HUECOS
                        SharpKml.Dom.Polygon polygon = new SharpKml.Dom.Polygon(); //se crea poligono
                        //Bucle outer
                        int j = 0;
                        int Max = Extr.Length;
                        SharpKml.Dom.OuterBoundary outerBoundary = new SharpKml.Dom.OuterBoundary();
                        outerBoundary.LinearRing = new SharpKml.Dom.LinearRing();
                        outerBoundary.LinearRing.Coordinates = new SharpKml.Dom.CoordinateCollection();
                        while (j < Max)
                        {
                            outerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Extr[j].Y, Extr[j].X));
                            j++;
                        }
                        polygon.OuterBoundary = outerBoundary; //se importan dentro del poligono las coordenadas SOLO DEL POLYGONO EXTERIOR!
                        SharpKml.Dom.Placemark placemark = new SharpKml.Dom.Placemark(); //se crea placemark
                        placemark.Name = tipo; //nombre de la area
                        placemark.Geometry = polygon; //añadir geometria al placemark

                        lista.Add(placemark);
                    }
                }
                else if (Area_Operaciones.GetType().ToString() == "NetTopologySuite.Geometries.MultiPolygon")
                {
                    foreach(Geometry GEO in ((MultiPolygon)Area_Operaciones).Geometries)
                    {
                        var Extr = ((Polygon)GEO).ExteriorRing.Coordinates;
                        if (((Polygon)GEO).InteriorRings.Length != 0)
                        {
                            //CASO CON HUECOS!!
                            SharpKml.Dom.Polygon polygon = new SharpKml.Dom.Polygon(); //se crea poligono
                                                                                       //Bucle inner
                            //int con = 1;
                            var Intr = ((Polygon)GEO).InteriorRings;
                            foreach (LineString Forat in Intr)
                            {
                                int x = 0;
                                int Maxx = Forat.Coordinates.Length;
                                SharpKml.Dom.InnerBoundary innerBoundary = new SharpKml.Dom.InnerBoundary();
                                innerBoundary.LinearRing = new SharpKml.Dom.LinearRing();
                                innerBoundary.LinearRing.Coordinates = new SharpKml.Dom.CoordinateCollection();
                                while (x < Maxx)
                                {
                                    innerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Forat.Coordinates[x].Y, Forat.Coordinates[x].X));
                                    x++;
                                }
                                polygon.AddInnerBoundary(innerBoundary);
                            }

                            //Bucle outer
                            int j = 0;
                            int Max = Extr.Length;
                            SharpKml.Dom.OuterBoundary outerBoundary = new SharpKml.Dom.OuterBoundary();
                            outerBoundary.LinearRing = new SharpKml.Dom.LinearRing();
                            outerBoundary.LinearRing.Coordinates = new SharpKml.Dom.CoordinateCollection();
                            while (j < Max)
                            {
                                outerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Extr[j].Y, Extr[j].X));
                                j++;
                            }
                            polygon.OuterBoundary = outerBoundary; //se importan dentro del poligono las coordenadas SOLO DEL POLYGONO EXTERIOR!
                            SharpKml.Dom.Placemark placemark = new SharpKml.Dom.Placemark(); //se crea placemark
                            if(tipo_multiple!=0) //nombre de la area
                                placemark.Name = tipo; 
                            placemark.Geometry = polygon; //añadir geometria al placemark

                            lista.Add(placemark);
                        }
                        else
                        {
                            //CASO SIN HUECOS
                            SharpKml.Dom.Polygon polygon = new SharpKml.Dom.Polygon(); //se crea poligono
                                                                                       //Bucle outer
                            int j = 0;
                            int Max = Extr.Length;
                            SharpKml.Dom.OuterBoundary outerBoundary = new SharpKml.Dom.OuterBoundary();
                            outerBoundary.LinearRing = new SharpKml.Dom.LinearRing();
                            outerBoundary.LinearRing.Coordinates = new SharpKml.Dom.CoordinateCollection();
                            while (j < Max)
                            {
                                outerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Extr[j].Y, Extr[j].X));
                                j++;
                            }
                            polygon.OuterBoundary = outerBoundary; //se importan dentro del poligono las coordenadas SOLO DEL POLYGONO EXTERIOR!
                            SharpKml.Dom.Placemark placemark = new SharpKml.Dom.Placemark(); //se crea placemark
                            placemark.Name = nombre; //nombre de la area
                            placemark.Geometry = polygon; //añadir geometria al placemark

                            lista.Add(placemark);
                        }
                    }

                    
                }

                nombre = nombre + "-" + tipo + "-" + FL;
                var Doc = new SharpKml.Dom.Document(); //se crea documento
                Doc.Name = nombre; //nombre documento
                foreach (SharpKml.Dom.Placemark placemark in lista)
                {
                    Doc.AddFeature(placemark); //añadir placermak dentro del documento
                }

                //Guardar Documento dentro del KML y exportar
                var kml = new SharpKml.Dom.Kml();
                kml.Feature = Doc; //DOCUMENTO
                                   //kml.Feature = placemark; //Se puede guardar directamente un placemark
                KmlFile kmlfile = KmlFile.Create(kml, true);

                //Eliminar archivo temporal
                if (File.Exists(Path.Combine(Path.Combine(@".\" + carpeta + "", nombre +".kmz"))))
                {
                    // If file found, delete it    
                    File.Delete(Path.Combine(Path.Combine(@".\" + carpeta + "", nombre + ".kmz")));
                }

                using (var stream = File.OpenWrite(Path.Combine(@".\" + carpeta + "", nombre + ".kmz"))) //Path de salida
                {
                    kmlfile.Save(stream);
                }

                return "exportado a "+Path.Combine(@".\" + carpeta + "", nombre + "_" + tipo + ".kmz")+"";
            } //Crea un KML SOLO con la cobertura asociada. No KML_Compuesto (multiples capas con, coberturas originales, simples, multiples i la total)
        }

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

            int Control_M = -1;
            while (Control_M != 0) //Menú principal
            {
                try
                {
                    Console.Clear();
                    Console.WriteLine("EXCAMUB");
                    Console.WriteLine();
                    Console.WriteLine("1 - Cálculo de multicoberturas");
                    Console.WriteLine("2 - Filtrado SACTA");
                    Console.WriteLine("3 - Cálculo de redundáncias");
                    Console.WriteLine("4 - Cálculo de cobertura mínima");
                    Console.WriteLine();
                    Console.WriteLine("0 - Finalizar");
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine("Introduzca identificador de operación (p.e. 1)");
                    Control_M = Convert.ToInt32(Console.ReadLine()); //Actualizar valor Control_M

                    //Ifs de control
                    if(Control_M == 1) //Cáclulo de multicobertura
                    {
                        List<SharpKml.Dom.Document> Docs = new List<SharpKml.Dom.Document>(); //Lista con archivos base kml para crear kmz final

                        //Variables de información al usuario
                        string NombrePredeterminado = ""; //String para guardar un nombre de proyecto predeterminado
                        string Directorio_IN = ""; //Directorio de entrada
                        List<string> NombresCargados = new List<string>(); //Lista donde se guardan los nombres de los archivos cargados.

                        bool parte1 = true; //Control de parte1
                        try //Parte1 - Cargar ficheros de entrada y ejecutar cálculos
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

                            List<Cobertura> Originales = new List<Cobertura>();
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
                                    FL = file.Name.Split(".")[0].Split('-')[1];
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

                                Originales.Add(new Cobertura(Nombre.Split('-')[0], FL, "original", Poligonos));

                                Console.WriteLine(Nombre);
                                NombresCargados.Add(Nombre);
                            }

                            //1.4 - Cálculos
                            //Originales
                            Conjunto conjunto = new Conjunto(Originales, "original", FL_IN);
                            //List<SharpKml.Dom.Document> Docs_folder = new List<SharpKml.Dom.Document>() //
                            foreach (Cobertura COB in conjunto.A_Operar)
                            {
                                Docs.Add(COB.CrearDocumentoSharpKML());
                                //COB.CrearKML_Simple("Resultados");
                            }

                            //Cobertura total
                            Cobertura CoberturaTotal = conjunto.FormarCoberturaTotal();
                            Docs.Add(CoberturaTotal.CrearDocumentoSharpKML());

                            //Coberturas multiples y multiples total
                            (List<Conjunto> Con, Conjunto Mul, Cobertura Cob) = conjunto.FormarCoberturasMultiples();
                            if (Cob != null)
                                Docs.Add(Cob.CrearDocumentoSharpKML());
                            foreach (Cobertura COB in Mul.A_Operar)
                            {
                                Docs.Add(COB.CrearDocumentoSharpKML());
                            }
                            foreach (Conjunto Conn in Con)
                            {
                                foreach (Cobertura COB in Conn.A_Operar)
                                {
                                    Docs.Add(COB.CrearDocumentoSharpKML());
                                }
                            }

                            //Coberturas simples
                            (Conjunto CoberturasSimples, Cobertura CoberturaMultipleTotal, Cobertura CoberturaSimpleTotal) = conjunto.FormarCoberturasSimples(Mul, Cob);
                            foreach (Cobertura COB in CoberturasSimples.A_Operar)
                            {
                                Docs.Add(COB.CrearDocumentoSharpKML());
                            }

                            Docs.Add(CoberturaSimpleTotal.CrearDocumentoSharpKML());

                            NombrePredeterminado = conjunto.Nombre_Resultado + "-" + conjunto.FL;

                            //Fin de la parte1
                        }
                        catch(Exception e)
                        {
                            Console.WriteLine(e.Message);
                            Console.ReadLine();
                            parte1 = false; //Informar de que la parte1 no se ha ejecutado correctamente
                        } //Parte1 - Cargar ficheros de entrada, FL y ejecutar cálculos

                        if(parte1) //Si y solo si la parte1 ha sido ejecutada con éxito ejecutamos la parte2
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

                            foreach (SharpKml.Dom.Document doc in Docs)
                            {
                                Doc.AddFeature(doc); //añadir placermak dentro del documento
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

          
          
            

            //Salida
            

            

            Console.WriteLine();

            

            

            //Forzar Huecos
            //Operaciones.CrearKMZconHuecos(Originales[5], Originales[1]);
            //Operaciones.CrearKMZconHuecos(Originales[4], Originales[0]);
            //Operaciones.CrearKMZconHuecos(Originales[3], Originales[2]);

            
            //CoberturaMultipleTotal.CrearKML_Simple("Resultados");

            
        }
    }
}

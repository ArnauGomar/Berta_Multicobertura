using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using SharpKml.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Berta
{
    public class Cobertura
    {
        public string nombre;
        public string FL;
        public string tipo; //Simple, Multiple, Original, Total...
        public int tipo_multiple = 0; //De que grado de multicobertura se trata si es que es tipo "multiple"

        public Geometry Area_Operaciones; //Area poligonal de la cobertura, donde se efectuan todos los cálculos

        public List<int> InterseccionesLista = new List<int>(); //Lista de 0,1 (y -1 cuando es la misma cobertura) para definir la intersección entre esta cobertura y las otras de un conjunto

        //Constructores
        /// <summary>
        /// Constructor simple
        /// </summary>
        public Cobertura()
        { } //Simple

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="Nombre"></param>
        /// <param name="Fl"></param>
        /// <param name="Tipo"></param>
        /// <param name="Poligono"></param>
        public Cobertura(string Nombre, string Fl, string Tipo, Geometry Poligono)
        {
            nombre = Nombre; ;
            FL = Fl;
            tipo = Tipo;

            Area_Operaciones = Poligono;

        } //tipo simple, original, total. CASO SIMPLE

        /// <summary>
        /// Constructor con múltiples poligonos
        /// </summary>
        /// <param name="Nombre"></param>
        /// <param name="Fl"></param>
        /// <param name="Tipo"></param>
        /// <param name="Poligono"></param>
        public Cobertura(string Nombre, string Fl, string Tipo, List<Geometry> Poligono)
        {
            nombre = Nombre; ;
            FL = Fl;
            tipo = Tipo;

            if (Poligono.Count == 1) //Si en la lista de poligonos solo hay uno lo guardamos directamente
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

        /// <summary>
        /// Constructor tipo multiple
        /// </summary>
        /// <param name="Nombre"></param>
        /// <param name="Fl"></param>
        /// <param name="Tipo"></param>
        /// <param name="Tipo_Multiple"></param>
        /// <param name="Poligono"></param>
        public Cobertura(string Nombre, string Fl, string Tipo, int Tipo_Multiple, Geometry Poligono)
        {
            nombre = Nombre; ;
            FL = Fl;
            tipo = Tipo;
            tipo_multiple = Tipo_Multiple;

            Area_Operaciones = Poligono;
        } //tipo multiple

        /// <summary>
        /// Guardaremos en una lista las intersecciones de la cobertura con un conjunto. 1 Intersecta, 0 No intersecta, -1 Misma cobertura
        /// </summary>
        /// <param name="A_Operar">Conjunto a comparar</param>
        public void GenerarListaIntersectados(List<Cobertura> A_Operar)
        {
            //Esta en orden de indices del A_Operar del conjuntoMadre (orden de carga)
            foreach (Cobertura cobertura in A_Operar)
            {
                //Para cada cobertura presente en el conjunto A_Operar miramos si es la misma geometria (-1), si intersecciona (1) o si no se solapan (0)
                //Despues añadimos a la InterseccionesLista
                Geometry G = Operaciones.ReducirPrecision(cobertura.Area_Operaciones);
                if (Operaciones.ReducirPrecision(this.Area_Operaciones).EqualsExact(G, 0.1))
                    InterseccionesLista.Add(-1);
                else if (Operaciones.ReducirPrecision(this.Area_Operaciones).Intersects(G) == true)
                    InterseccionesLista.Add(1);
                else
                    InterseccionesLista.Add(0);
            }
        } //Lista de intersecciones 

        /// <summary>
        /// Actualización de area_operaciones
        /// </summary>
        /// <param name="New"> Area para actualizar</param>
        public void ActualizarAreas(Geometry New)
        {
            Area_Operaciones = New;
        } //Actualización de area_operaciones

        /// <summary>
        /// Forma el nombre en el fromato deseado por Enaire
        /// </summary>
        /// <returns></returns>
        private string Nombre_exp()
        {
            string r;
            if (tipo == "total") //Si es del tipo total será la cobertura total de todo el conjunto
                r = "Cobertura Total " + this.FL;
            else if (tipo == "multiple total") //Se trata de la cobertura anillo de un lvl en concreto
                r = string.Format("{0:00}", tipo_multiple) + " Total";
            else if (tipo == "simple total") //cobertura anillo de lvl 1 (simple)
                r = string.Format("{0:00}", 1) + " Total";
            else //Multicoberuras distintas, dependiendo de los radares participantes
            {
                //Reescriptura del nombre a nuevo formato propuesto por Enaire
                List<string> V = nombre.Split(" () ").ToList();
                V = V.OrderBy(x => x).ToList(); //Ordenar alfabeticamente
                nombre = string.Join('.', V);
                if (V.Count == 1)
                    r = nombre + " " + this.FL;
                else
                    r = nombre;
            }


            return r;
        } //Forma el nombre en el fromato deseado por Enaire

        /// <summary>
        /// Definir formato de capa
        /// </summary>
        /// <returns></returns>
        private SharpKml.Dom.Style Estilo()
        {
            SharpKml.Dom.Style style = new SharpKml.Dom.Style();
            SharpKml.Dom.PolygonStyle pstyle = new SharpKml.Dom.PolygonStyle();
            if ((tipo == "original") || (tipo == "total") || (tipo == "simple") || (tipo == "simple total"))
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
        } //Definir formato de capa

        /// <summary>                   
        /// Traduce de coordenadas NetTopologySuite a coordenadas SharpKML y guarda la infromación asociada en un Documento SharkKML (NO KML)
        /// </summary>
        /// <returns></returns>
        public SharpKml.Dom.Document CrearDocumentoSharpKML()                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        
        {
            List<SharpKml.Dom.Placemark> lista = new List<SharpKml.Dom.Placemark>();

            //Mirar si es un poligono o un multipoligono y obtener transformación de librerias 
            if (Area_Operaciones.GetType().ToString() == "NetTopologySuite.Geometries.Polygon") //Se trata de una cobertura con un solo poligono
            {
                var Extr = ((Polygon)Area_Operaciones).ExteriorRing.Coordinates; //Extraer coordenadas externas
                if (((Polygon)Area_Operaciones).InteriorRings.Length != 0) //Si existen anillos de coordenadas internos (huecos)
                {
                    //CASO CON HUECOS!!
                    SharpKml.Dom.Polygon polygon = new SharpKml.Dom.Polygon(); //se crea poligono
                                                                               //Bucle inner
                    var Intr = ((Polygon)Area_Operaciones).InteriorRings;
                    foreach (LineString Forat in Intr) //Bucle para traducir coordenadas (inner)
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
                        polygon.AddInnerBoundary(innerBoundary); //Se añade al poligono
                    }

                    //Bucle outer
                    int j = 0;
                    int Max = Extr.Length;
                    SharpKml.Dom.OuterBoundary outerBoundary = new SharpKml.Dom.OuterBoundary();
                    outerBoundary.LinearRing = new SharpKml.Dom.LinearRing();
                    outerBoundary.LinearRing.Coordinates = new SharpKml.Dom.CoordinateCollection();
                    while (j < Max) //Bucle para traducir coordenadas (outer)
                    {
                        outerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Extr[j].Y, Extr[j].X));
                        j++;
                    }
                    polygon.OuterBoundary = outerBoundary; //se importan dentro del poligono las coordenadas SOLO DEL POLYGONO EXTERIOR!
                    SharpKml.Dom.Placemark placemark = new SharpKml.Dom.Placemark(); //se crea placemark
                    placemark.Name = Nombre_exp(); //generamos nuevo nombre Enaire
                    placemark.Geometry = polygon; //añadir geometria al placemark
                    placemark.AddStyle(Estilo()); //Creamos estilo pertinente

                    lista.Add(placemark); //Añadimos al listado de placemarks para poder guardarlo al final
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
                    while (j < Max)//Bucle para traducir coordenadas (inner)
                    {
                        outerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Extr[j].Y, Extr[j].X));
                        j++;
                    }
                    polygon.OuterBoundary = outerBoundary; //se importan dentro del poligono las coordenadas SOLO DEL POLYGONO EXTERIOR!
                    SharpKml.Dom.Placemark placemark = new SharpKml.Dom.Placemark(); //se crea placemark
                    placemark.Name = Nombre_exp(); //generamos nuevo nombre Enaire
                    placemark.Geometry = polygon; //añadir geometria al placemark
                    placemark.AddStyle(Estilo()); //Creamos estilo pertinente

                    lista.Add(placemark); //Añadimos al listado de placemarks para poder guardarlo al final
                }
            }
            else if (Area_Operaciones.GetType().ToString() == "NetTopologySuite.Geometries.MultiPolygon") //Se trata de una cobertura con multiples poligonos
            {
                foreach (Geometry GEO in ((MultiPolygon)Area_Operaciones).Geometries) //Para todas las geometrias dentro de Area_Operaciones
                {
                    var Extr = ((Polygon)GEO).ExteriorRing.Coordinates;
                    if (((Polygon)GEO).InteriorRings.Length != 0)
                    {
                        //CASO CON HUECOS!!
                        SharpKml.Dom.Polygon polygon = new SharpKml.Dom.Polygon(); //se crea poligono
                                                                                   //Bucle inner
                                                                                   //int con = 1;
                        var Intr = ((Polygon)GEO).InteriorRings;
                        foreach (LineString Forat in Intr) //Forat = Agujero, extraemos coordenadas de anillos internos (huecos) y lo traducimos a SharpKML 
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
                            polygon.AddInnerBoundary(innerBoundary); //Se añade anillo interno al poligono
                        }

                        //Bucle outer
                        int j = 0;
                        int Max = Extr.Length;
                        SharpKml.Dom.OuterBoundary outerBoundary = new SharpKml.Dom.OuterBoundary();
                        outerBoundary.LinearRing = new SharpKml.Dom.LinearRing();
                        outerBoundary.LinearRing.Coordinates = new SharpKml.Dom.CoordinateCollection();
                        while (j < Max) //Traducimos anillo externo
                        {
                            outerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Extr[j].Y, Extr[j].X));
                            j++;
                        }
                        polygon.OuterBoundary = outerBoundary; //se importan dentro del poligono las coordenadas SOLO DEL POLYGONO EXTERIOR!
                        SharpKml.Dom.Placemark placemark = new SharpKml.Dom.Placemark(); //se crea placemark
                        placemark.Name = Nombre_exp();
                        placemark.Geometry = polygon; //añadir geometria al placemark
                        placemark.AddStyle(Estilo());

                        lista.Add(placemark); //Añadimos al listado de placemarks para poder guardarlo al final
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
                        while (j < Max)//Traducimos
                        {
                            outerBoundary.LinearRing.Coordinates.Add(new SharpKml.Base.Vector(Extr[j].Y, Extr[j].X));
                            j++;
                        }
                        polygon.OuterBoundary = outerBoundary; //se importan dentro del poligono las coordenadas SOLO DEL POLYGONO EXTERIOR!
                        SharpKml.Dom.Placemark placemark = new SharpKml.Dom.Placemark(); //se crea placemark
                        placemark.Name = Nombre_exp();
                        placemark.Geometry = polygon; //añadir geometria al placemark
                        placemark.AddStyle(Estilo());

                        lista.Add(placemark);//Añadimos al listado de placemarks para poder guardarlo al final
                    }
                }
            }

            var Doc = new SharpKml.Dom.Document(); //se crea documento que se guardara en el Main dentro de una carpeta
            Doc.Name = Nombre_exp(); //Nombre del doc igual que el del poligono 
            foreach (SharpKml.Dom.Placemark placemark in lista)
            {
                placemark.Visibility = false; //Inicialmente invisible 
                Doc.AddFeature(placemark); //añadir placermak dentro del documento
            }
            Doc.Visibility = false; //Inicialmente invisible 

            return Doc;
        } //Traduce de NetTopologySuite a SharpKML y guarda la infromación asociada en un Documento SharkKML (NO KML) para despues ser
                                                                 //guardado en un documento que agrupara todas las carpetas
        /// <summary>
        /// Crear KML simple (solo la cobertura en cuestión y lo guarda en la carpeta indicada
        /// </summary>
        /// <param name="carpeta"> path de la carpeta a exportar</param>
        /// <returns></returns>
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
                    placemark.Name = Nombre_exp(); //generamos nuevo nombre Enaire
                    placemark.Geometry = polygon; //añadir geometria al placemark
                    placemark.AddStyle(Estilo()); //Creamos estilo pertinente
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
                    placemark.Name = Nombre_exp(); //generamos nuevo nombre Enaire
                    placemark.Geometry = polygon; //añadir geometria al placemark
                    placemark.AddStyle(Estilo()); //Creamos estilo pertinente
                    placemark.Geometry = polygon; //añadir geometria al placemark


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
                        placemark.Geometry = polygon; //añadir geometria al placemark
                        placemark.Name = Nombre_exp(); //generamos nuevo nombre Enaire
                        placemark.Geometry = polygon; //añadir geometria al placemark
                        placemark.AddStyle(Estilo()); //Creamos estilo pertinente

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
                        placemark.Name = Nombre_exp(); //generamos nuevo nombre Enaire
                        placemark.Geometry = polygon; //añadir geometria al placemark
                        placemark.AddStyle(Estilo()); //Creamos estilo pertinente

                        lista.Add(placemark);
                    }
                }


            }

            nombre = Nombre_exp()+"-"+this.FL;
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
            if (File.Exists(Path.Combine(Path.Combine(@".\" + carpeta + "", nombre + ".kmz"))))
            {
                // If file found, delete it    
                File.Delete(Path.Combine(Path.Combine(@".\" + carpeta + "", nombre + ".kmz")));
            }
            string p = Path.Combine(carpeta, nombre + ".kmz");
            using (var stream = File.OpenWrite(p)) //Path de salida
            {
                kmlfile.Save(stream);
            }

            return Path.Combine(@".\" + carpeta + "", nombre +".kmz") + "";
        } //Crea un KML SOLO con la cobertura asociada. Herramienta útil para la visualización durante el dev
    }
}

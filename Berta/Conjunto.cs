using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;
using SharpKml.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Berta
{
    /// <summary>
    /// Clase que representa una coleccion de coberturas, conjunto de coberturas. 
    /// </summary>
    public class Conjunto
    {
        public List<Cobertura> A_Operar = new List<Cobertura>(); //Lista de coberturas
        public string Identificador; //Identificador (opcional)
        public string FL; //FL del conjunto
        public List<string> Combinaciones = new List<string>(); //Numero de combinaciones generadoras de intersecciones

        /// <summary>
        /// Constructor simple
        /// </summary>
        public Conjunto()
        { }

        /// <summary>
        /// Constructor complejo
        /// </summary>
        /// <param name="Coberturas"></param>
        /// <param name="ID"></param>
        /// <param name="Fl"></param>
        public Conjunto(List<Cobertura> Coberturas, string ID, string Fl)
        {
            A_Operar = Coberturas;
            Identificador = ID;
            FL = Fl;
        }// Extrae de las coberturas entradas las areas para poder trabajar con ellas, genera nombre (inicalmente en null)

        /// <summary>
        /// Generar combinaciones para ejecutar las intersecciones
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private IEnumerable<IEnumerable<T>> Combinacion<T>(IEnumerable<T> items, int count)
        {
            //Count = numero de elementos que se quieren en la combinación
            int i = 0;
            foreach (var item in items)
            {
                if (count == 1) //Si solo se quiere un elemento retornamos el item en cada paso
                    yield return new T[] { item };
                else
                {
                    foreach (var result in Combinacion(items.Skip(i + 1), count - 1))
                        yield return new T[] { item }.Concat(result);
                }

                ++i;
            }
        }//Permutar nombres, retorna 

        /// <summary>
        /// Desestima las combinaciones que no darán un resultado (que su intersección no existe)
        /// </summary>
        public void FiltrarCombinaciones()
        {
            ProgressBar PB = new ProgressBar();

            //Generar lista de interseccion dos a dos de cada cobertura
            foreach (Cobertura cobertura in this.A_Operar)
            {
                cobertura.GenerarListaIntersectados(this.A_Operar);
            }

            int MultipleMax = A_Operar.Count; //El número de radares determina la cobertura múltiple máxima possible

            List<string> Nombres = new List<string>();
            foreach (Cobertura c in this.A_Operar)
                Nombres.Add(c.nombre);
            int Pro = 1; int Max = MultipleMax;
            while (MultipleMax > 1)
            {
                IEnumerable<IEnumerable<string>> GenCombinaciones = Combinacion(Nombres, MultipleMax); //Extraer todas las combinaciones posibles (sin repetición) para cada lvl de multicoberutra
                                                                                                         //llamado Combinación general
                foreach (IEnumerable<string> Combinacions in GenCombinaciones)
                {
                    IEnumerable<IEnumerable<string>> GenCombinaciones_2 = Combinacion(Combinacions, 2); //Ejecutar la combinación 2 a 2 de los participantes de la combinación general

                    bool Intersecciona = true;
                    foreach (IEnumerable<string> Combinacion_2 in GenCombinaciones_2)
                    {
                        //Extraer indice en la lista de intersecciones del seegundo elemento de la combinación para mirar si intersecta con el primer elemento
                        int IndexOfLast = A_Operar.IndexOf(A_Operar.Where(x => x.nombre == Combinacion_2.Last()).ToList()[0]);
                        int ValorInt = A_Operar.Where(x => x.nombre == Combinacion_2.First()).ToList()[0].InterseccionesLista[IndexOfLast]; //Mirar intersección, 1 interseccion, 0 no intersección 
                        if (ValorInt == 0) //El par no intersecciona por lo que no hay posibilidad de intersección real. 
                        {
                            Intersecciona = false;
                            break;
                        }

                    }
                    if (Intersecciona == true)
                        Combinaciones.Add(string.Join(" () ", Combinacions));
                }
                MultipleMax--;
                PB.Report((double)Pro / Max);
                Pro++;
            }
            PB.Dispose();
        } //Elimina las combinaciones que no intersectan

        /// <summary>
        /// Ejecuta la unión de todo el conjunto para retornar la cobertura total (suma) de todas las coberturas del conjunto
        /// </summary>
        /// <returns></returns>
        public Cobertura FormarCoberturaTotal()
        {
            ICollection<Geometry> Areas = new List<Geometry>();

            int i = 1;
            string N = A_Operar[0].nombre.Split('-')[0];
            List<string> NN = new List<string>();
            NN.Add(N);
            Areas.Add(A_Operar[0].Area_Operaciones);

            while (i < A_Operar.Count())
            {
                Areas.Add(A_Operar[i].Area_Operaciones);
                NN.Add(A_Operar[i].nombre);
                N = N + " () " + A_Operar[i].nombre.Split('-')[0];
                i++;
            }

            CascadedPolygonUnion ExecutarUnion = new CascadedPolygonUnion(Areas);
            var GEO = ExecutarUnion.Union(); //geometria a retornar 

            return new Cobertura(N, this.FL, "total", GEO);
        } //Union de todas las coberturas

        /// <summary>
        /// Ejecuta las intersecciones siguiendo las combinaciones filtradas
        /// </summary>
        /// <param name="Umbral"></param>
        /// <returns></returns>
        private List<Conjunto> FormarCoberturasMultiples_Paso1(double Umbral) //Conjuntos por lvl
        {
            List<Conjunto> Conjuntos = new List<Conjunto>(); //Guardaremos los conjuntos (por nivel) de multicoberturas
            ProgressBar PB = new ProgressBar();

            int Pro = 0; 
            int MaxPro = Combinaciones.Count;
            foreach (string combinacion in Combinaciones)
            {
                List<string> PermutaList = combinacion.Split(" () ").ToList();
                int MultipleMax = PermutaList.Count();

                List<Cobertura> Operables = A_Operar.Where(x => PermutaList.Contains(x.nombre)).ToList();//Lista para guardar las coberturas indicadas por la permutación

                //Cáclulo de intersección (REC/BUSC) for por intentar mejorar el rendimiento
                Geometry BASE = Operables[0].Area_Operaciones;
                int Count = Operables.Count;
                for (int i = 1; i < Count; i++)
                {
                    BASE = BASE.Intersection(Operables[i].Area_Operaciones);
                    if ((BASE.IsEmpty) || (BASE.Area < Umbral)) //Si el poligono es empty o su area es inferior a Umbral paramos cálculo
                        break;
                }

                //Buscar conjunto de coberturas del mismo nivel, si no existe crearlo
                if ((BASE.IsEmpty == false)&& (BASE.Area >= Umbral))
                {
                    //Si el resultado no es nulo (No interseccionan por lo que el resultado es nulo) creamos nueva cobertura y añadimos al conjunto
                    Cobertura A_Guardar = new Cobertura(combinacion, this.FL, "multi", MultipleMax, BASE);

                    //Buscar conjunto (indice de)
                    List<Conjunto> Extraido = Conjuntos.Where(x => x.Identificador == "multi " + MultipleMax).ToList();
                    if (Extraido.Count != 0)
                    {
                        Conjuntos[Conjuntos.IndexOf(Extraido[0])].A_Operar.Add(A_Guardar);
                        //Conjuntos[Conjuntos.IndexOf(Extraido[0])].EjecutarConstrucción();
                    }
                    else
                    {
                        //Inicializar un nuevo conjunto
                        Conjunto A_Guardar_Conjunto = new Conjunto();
                        A_Guardar_Conjunto.FL = this.FL;
                        A_Guardar_Conjunto.Identificador = "multi " + MultipleMax;
                        A_Guardar_Conjunto.A_Operar.Add(A_Guardar);
                        //A_Guardar_Conjunto.EjecutarConstrucción();
                        Conjuntos.Add(A_Guardar_Conjunto);
                    }
                }
                PB.Report((double)Pro/ MaxPro);
                Pro++;
            }
            PB.Dispose();
            return Conjuntos;
        }

        /// <summary>
        /// Busca si existe cobertura máxima del conjunto (lvl max = num de coberturas)
        /// </summary>
        /// <param name="CoberturasPorLvl"></param>
        /// <returns></returns>
        private (Cobertura, bool) FormarCoberturasMultiples_Paso2(List<Conjunto> CoberturasPorLvl) //Calcular cobertura máxima
        {
            int MultipleMax = this.A_Operar.Count; //El número de radares determina la cobertura múltiple máxima possible

            Cobertura MAX = new Cobertura();
            bool CobMax = false;

            if (CoberturasPorLvl.First().A_Operar.First().tipo_multiple == MultipleMax)
            {
                MAX = CoberturasPorLvl.First().A_Operar.First();
                //MAX.nombre = "(MAX)";
                CobMax = true;
            }

            if (CobMax)
                return (MAX, true);
            else
                return (null, false);
        }

        /// <summary>
        /// Crea los anillos por nivel y aplica la diferencia de multi-coberturas de nivel superior a las sobrepuestas de nivel inferior.
        /// </summary>
        /// <param name="CoberturasPorLvl"></param>
        /// <param name="CoberturaMAX"></param>
        /// <param name="epsilon"></param>
        /// <param name="Umbral"></param>
        /// <returns></returns>
        private (Conjunto, List<Conjunto>) FormarCoberturasMultiples_Paso3(List<Conjunto> CoberturasPorLvl, bool CoberturaMAX, double epsilon, double Umbral) //Cálcula anillos de coberturas del mismo lvl
        {
            //Dos casos, hay multiple máx o no. Si la hay se ejecuta como en version Alpha
            List<Cobertura> TotalPorLVL = new List<Cobertura>(); //Lista para guardar la unión por lvl de multicobertura (anillos)
            var gff = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(); //Factoria para crear todos los multipoligonos o poligonos del codigo
            ProgressBar PB = new ProgressBar();

            if (CoberturaMAX) //Hay cobertura max
            {
                List<Polygon> Verificados = new List<Polygon>(); //Lista de verificación de poligonos (fitrado y umbral)
                var GEO_Resta = Operaciones.ReducirPrecision(CoberturasPorLvl.First().A_Operar.First().Area_Operaciones); //Area que restaremos a todas las múlticoberturas para así mostrar correctamente el resultado

                int j = 1; //El primero no se calcula (MAX)
                while (j < CoberturasPorLvl.Count)
                {
                    foreach (Cobertura Cob in CoberturasPorLvl[j].A_Operar)
                    {
                        //Restar
                        var CobRound = Operaciones.ReducirPrecision(Cob.Area_Operaciones);
                        var NewGeo = Operaciones.ReducirPrecision(CobRound.Difference(GEO_Resta));
                        Cob.ActualizarAreas(NewGeo); //Actualizar
                    }
                    CoberturasPorLvl[j].A_Operar.RemoveAll(x => x.Area_Operaciones.IsEmpty == true);

                    foreach (Cobertura Cob in CoberturasPorLvl[j].A_Operar)
                    {
                        //Restar
                        var NewGeo = Operaciones.ReducirPrecision(Cob.Area_Operaciones.Difference(GEO_Resta));
                        //NewGeo = Operaciones.EliminarFormasExtrañas(NewGeo);
                        if ((NewGeo.Area < epsilon) || (NewGeo.Area < Umbral)) //Eliminar geometrias sospechosas
                        {
                            NewGeo = gff.CreateEmpty(Dimension.Curve);
                        }
                        else if (NewGeo.GetType().ToString() != "NetTopologySuite.Geometries.Polygon")
                        {
                            MultiPolygon Verificar = (MultiPolygon)NewGeo;
                            var Poligonos = Verificar.Geometries;
                            Verificados = new List<Polygon>();
                            foreach (Polygon Poly in Poligonos)
                            {
                                if ((Poly.Area > epsilon) && (Poly.Area >= Umbral))
                                {
                                    Verificados.Add(Poly);
                                }
                            }
                            Verificados.RemoveAll(x => x.IsEmpty == true);

                            NewGeo = gff.CreateMultiPolygon(Verificados.ToArray());
                        }

                        Cob.ActualizarAreas(NewGeo); //Actualizar
                    }


                    var UnionLvl = Operaciones.ReducirPrecision(CoberturasPorLvl[j].FormarCoberturaTotal().Area_Operaciones);

                    Geometry Resta = UnionLvl.Difference(GEO_Resta);
                    TotalPorLVL.Add(new Cobertura("", this.FL, "multiple total", CoberturasPorLvl[j].A_Operar[0].tipo_multiple, Resta)); //Se crea el anillo
                    GEO_Resta = Operaciones.ReducirPrecision(GEO_Resta.Union(UnionLvl));
                    TotalPorLVL.RemoveAll(x => x.Area_Operaciones.IsEmpty == true);//Eliminamos coberturas vacias

                    PB.Report((double)j / CoberturasPorLvl.Count);
                    j++;
                }
            }
            else //No hay cobertura max
            {
                //Formar Geo_Resta, para hacer la diferencia sobre las otras capas
                Geometry GEO_Resta = Operaciones.ReducirPrecision(CoberturasPorLvl.First().FormarCoberturaTotal().Area_Operaciones);
                TotalPorLVL.Add(new Cobertura("", this.FL, "multiple total", CoberturasPorLvl.First().A_Operar.First().tipo_multiple, GEO_Resta)); //Guardamos el primer anillo 

                //Geo_Resta inicialmente es la unión de todas las coberturas de lvl máximo (que no cobertura de lvl máximo, ya que en este caso no existe)
                //Ahora unimos cada anillo y ejecutamos la resta para obtener el resultado correcto
                int j = 1;
                while (j < CoberturasPorLvl.Count)
                {
                    //Crear anillo
                    var NewG = Operaciones.ReducirPrecision(CoberturasPorLvl[j].FormarCoberturaTotal().Area_Operaciones); //Ejecutamos unión de conjunto
                    Geometry Resta = NewG.Difference(GEO_Resta); //Hacemos la diferencia 
                    TotalPorLVL.Add(new Cobertura("", this.FL, "multiple total", CoberturasPorLvl[j].A_Operar[0].tipo_multiple, Resta)); //Guardamos anillo resultante

                    foreach (Cobertura Cob in CoberturasPorLvl[j].A_Operar)
                    {
                        //Restar
                        var CobRound = Operaciones.ReducirPrecision(Cob.Area_Operaciones);
                        var NewGeo = Operaciones.ReducirPrecision(CobRound.Difference(GEO_Resta));
                        if ((NewGeo.Area < epsilon) || (NewGeo.Area < Umbral))  //Eliminar geometrias sospechosas o eliminar geometrias discriminadas (se deja epsilon por si umbral se reduce mucho)
                        {
                            NewGeo = gff.CreateEmpty(Dimension.Curve);
                        }
                        else if (NewGeo.GetType().ToString() == "NetTopologySuite.Geometries.MultiPolygon")
                        {
                            //Eliminar geometrias sospechosas de un multipoligono
                            MultiPolygon Verificar = (MultiPolygon)NewGeo;
                            var Poligonos = Verificar.Geometries;
                            List<Polygon> Verificados = new List<Polygon>();

                            foreach (Polygon Poly in Poligonos)
                            {
                                if ((Poly.Area > epsilon) && (Poly.Area >= Umbral)) //TST 0.00001, eliminar poligonos por debajo de epsilon (VERSIÓN FINAL: Eliminar si no superan el umbral
                                {                                                   // se deja epsilon por si umbral se reduce mucho)
                                    Verificados.Add(Poly);
                                }
                            }
                            Verificados.RemoveAll(x => x.IsEmpty == true);

                            NewGeo = gff.CreateMultiPolygon(Verificados.ToArray());
                        }

                        Cob.ActualizarAreas(NewGeo); //Actualizar
                    }

                    GEO_Resta = NewG.Union(GEO_Resta); //Actualizamos area de resta (GEO_resta)

                    CoberturasPorLvl[j].A_Operar.RemoveAll(x => x.Area_Operaciones.IsEmpty == true);//Eliminamos coberturas vacias

                    PB.Report((double)j / CoberturasPorLvl.Count);
                    j++;
                }
            }
            PB.Dispose();

            if (TotalPorLVL.Count != 0)
            {
                Conjunto Anillos = new Conjunto(TotalPorLVL, "Total por lvl", this.FL); //Generamos un conjunto con todos los anillos
                return (Anillos, CoberturasPorLvl);
            }
            else
            {
                //TotalPorLVL.Add(new Cobertura("NaN", "FL999", "Original", gff.CreateEmpty(Dimension.Curve))); //Fake 
                //Conjunto Anillos = new Conjunto(TotalPorLVL, "Total por lvl", this.FL); //Generamos un conjunto con todos los anillos
                return (null, CoberturasPorLvl); //El elemento totalPorLvl solo sera nulo en calculos de coberturas simples
            }
                
        }

        /// <summary>
        /// Ejecuta los 3 steps del cálculo de multi-cobertura
        /// </summary>
        /// <param name="epsilon"></param>
        /// <param name="Umbral"></param>
        /// <returns></returns>
        public (List<Conjunto>, Conjunto, Cobertura) FormarCoberturasMultiples(double epsilon, double Umbral)
        {
            List<Conjunto> ConjuntosPorLvl = FormarCoberturasMultiples_Paso1(Umbral); //Ejecutar paso 1 

            (Cobertura CoberturaMaxima, bool existeMax) = FormarCoberturasMultiples_Paso2(ConjuntosPorLvl); //Ejecutar paso 2

            (Conjunto Anillos, List<Conjunto> ConjuntosPorLvl_F) = FormarCoberturasMultiples_Paso3(ConjuntosPorLvl, existeMax, epsilon, Umbral);

            if (existeMax)
            {
                ConjuntosPorLvl.RemoveAt(0); //Eliminamos cobertura máxima ya que esta presente en otro parámetro
                return (ConjuntosPorLvl_F, Anillos, CoberturaMaxima);
            }
            else
                return (ConjuntosPorLvl_F, Anillos, null); //Retornamos null la cobertura máxima
        }

        /// <summary>
        /// Calcula las coberturas simples del conjunto (de nivel 1), también crea el anillo simple. Retorna tambien el area comprendida por los niveles superiores
        /// </summary>
        /// <param name="TotalPorLvl"></param>
        /// <param name="Max"></param>
        /// <param name="epsilon_simple"></param>
        /// <param name="Umbral"></param>
        /// <returns></returns>
        public (Conjunto, Cobertura, Cobertura) FormarCoberturasSimples(Conjunto TotalPorLvl, Cobertura Max, double epsilon_simple, double Umbral)
        {

            Cobertura InterseccionTotal = new Cobertura();
            InterseccionTotal.FL = this.FL;
            InterseccionTotal.nombre = "multiple";
            InterseccionTotal.tipo = "total";
            InterseccionTotal.tipo_multiple = 0;

            if (Max != null) //Caso con cobertura máxima existente
            {
                //Primero unimos todas las intersecciones para asi crear la cobertura de intersección total 
                var GEOt = Operaciones.ReducirPrecision(Max.Area_Operaciones);

                if (TotalPorLvl != null) //El elemento totalPorLvl solo sera nulo en calculos de coberturas simples
                {
                    foreach (Cobertura cob in TotalPorLvl.A_Operar)
                    {
                        Geometry C = cob.Area_Operaciones;
                        var P_GEOt = GEOt.Union(C);
                        GEOt = Operaciones.ReducirPrecision(P_GEOt);
                    }
                }

                InterseccionTotal.ActualizarAreas(GEOt);
            }
            else //Caso donde cobertura máxima no existe
            {
                //Primero unimos todas las intersecciones para asi crear la cobertura de intersección total 
                var GEOt = Operaciones.ReducirPrecision(TotalPorLvl.A_Operar.First().Area_Operaciones);
                int j = 1;
                while (j < TotalPorLvl.A_Operar.Count)
                {
                    GEOt = Operaciones.ReducirPrecision(GEOt.Union(TotalPorLvl.A_Operar[j].Area_Operaciones));
                    j++;
                }
                InterseccionTotal.ActualizarAreas(GEOt);
            }

            List<Polygon> Verificados = new List<Polygon>(); //Lista de verificación de poligonos
            var gff = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(); //Factoria para crear multipoligonos

            //Crear simple total y filtrar por umbral
            Cobertura SimplesTotal = new Cobertura("", this.FL, "simple total", FormarCoberturaTotal().Area_Operaciones.Difference(InterseccionTotal.Area_Operaciones));
            //Depende de si es poligono o multipoligono, si es poligono seguro será mayor que el umbral,
            //ya que no es un caso habitual que un anillo de cobertura sea un solo poligono
            if(SimplesTotal.Area_Operaciones.GetType().ToString() == "NetTopologySuite.Geometries.MultiPolygon")
            {
                foreach (Polygon TrozoAnillo in (MultiPolygon)SimplesTotal.Area_Operaciones)
                {
                    if (TrozoAnillo.Area >= Umbral)
                        Verificados.Add(TrozoAnillo);
                }
                SimplesTotal.ActualizarAreas(gff.CreateMultiPolygon(Verificados.ToArray()));
            }


            //Restar para cada original la intersección total y asi obtener las coberutras simples 
            List<Cobertura> Ret = new List<Cobertura>();

            foreach (Cobertura COB in A_Operar)
            {
                var GEO = Operaciones.ReducirPrecision(COB.Area_Operaciones.Difference(InterseccionTotal.Area_Operaciones));

                if ((GEO.Area < epsilon_simple) || (GEO.Area < Umbral)) //Eliminar geometrias sospechosas y aplicar umbral
                {
                    GEO = gff.CreateEmpty(Dimension.Curve);
                }
                else if (GEO.GetType().ToString() == "NetTopologySuite.Geometries.MultiPolygon")
                {
                    //Eliminar geometrias sospechosas de un multipoligono
                    MultiPolygon Verificar = (MultiPolygon)GEO;
                    var Poligonos = Verificar.Geometries;
                    Verificados = new List<Polygon>();
                    gff = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();
                    foreach (Polygon Poly in Poligonos)
                    {
                        if ((Poly.Area > epsilon_simple) && (Poly.Area >= Umbral)) //Eliminar geometrias sospechosas y aplicar umbral
                        {
                            Verificados.Add(Poly);
                        }
                    }
                    Verificados.RemoveAll(x => x.IsEmpty == true);

                    GEO = gff.CreateMultiPolygon(Verificados.ToArray());
                }

                Ret.Add(new Cobertura(COB.nombre, this.FL, "simple", GEO));
            }

            Conjunto Simples = new Conjunto(Ret, "Coberturas simples", this.FL);
            Simples.A_Operar.RemoveAll(x => x.Area_Operaciones.IsEmpty == true);//Eliminamos coberturas vacias
            return (Simples, InterseccionTotal, SimplesTotal);

        } //coberturas simples (por cada radar y el conjunto de ellas) y intersección total (union de todo eso que no es múltiple)

        /// <summary>
        /// Aplica el filtro sacta a todo el conjunto
        /// </summary>
        /// <param name="SACTA"></param>
        /// <returns></returns>
        public Conjunto Aplicar_SACTA(Cobertura SACTA)
        {
            List<Cobertura> Filtradas = new List<Cobertura>();

            foreach(Cobertura Cob in this.A_Operar)
            {
                Geometry Filtrada = Cob.Area_Operaciones.Difference(SACTA.Area_Operaciones);

                Filtradas.Add(new Cobertura(Cob.nombre, Cob.FL, "original", Filtrada));
            }

            Conjunto R = new Conjunto(Filtradas, "Filtradas", "FL999");

            return R;
        }
    }

    /// <summary>
    /// Clase para integrar un ProgresBar al menú
    /// </summary>
    public class ProgressBar : IDisposable, IProgress<double>
    {
        private const int blockCount = 10;
        private readonly TimeSpan animationInterval = TimeSpan.FromSeconds(1.0 / 8);
        private const string animation = @"|/-\";

        private readonly Timer timer;

        private double currentProgress = 0;
        private string currentText = string.Empty;
        private bool disposed = false;
        private int animationIndex = 0;

        public ProgressBar()
        {
            timer = new Timer(TimerHandler);

            // A progress bar is only for temporary display in a console window.
            // If the console output is redirected to a file, draw nothing.
            // Otherwise, we'll end up with a lot of garbage in the target file.
            if (!Console.IsOutputRedirected)
            {
                ResetTimer();
            }
        }

        public void Report(double value)
        {
            // Make sure value is in [0..1] range
            value = Math.Max(0, Math.Min(1, value));
            Interlocked.Exchange(ref currentProgress, value);
        }

        private void TimerHandler(object state)
        {
            lock (timer)
            {
                if (disposed) return;

                int progressBlockCount = (int)(currentProgress * blockCount);
                int percent = (int)(currentProgress * 100);
                string text = string.Format("[{0}{1}] {2,3}% {3}",
                    new string('#', progressBlockCount), new string('-', blockCount - progressBlockCount),
                    percent,
                    animation[animationIndex++ % animation.Length]);
                UpdateText(text);

                ResetTimer();
            }
        }

        private void UpdateText(string text)
        {
            // Get length of common portion
            int commonPrefixLength = 0;
            int commonLength = Math.Min(currentText.Length, text.Length);
            while (commonPrefixLength < commonLength && text[commonPrefixLength] == currentText[commonPrefixLength])
            {
                commonPrefixLength++;
            }

            // Backtrack to the first differing character
            StringBuilder outputBuilder = new StringBuilder();
            outputBuilder.Append('\b', currentText.Length - commonPrefixLength);

            // Output new suffix
            outputBuilder.Append(text.Substring(commonPrefixLength));

            // If the new text is shorter than the old one: delete overlapping characters
            int overlapCount = currentText.Length - text.Length;
            if (overlapCount > 0)
            {
                outputBuilder.Append(' ', overlapCount);
                outputBuilder.Append('\b', overlapCount);
            }

            Console.Write(outputBuilder);
            currentText = text;
        }

        private void ResetTimer()
        {
            timer.Change(animationInterval, TimeSpan.FromMilliseconds(-1));
        }

        public void Dispose()
        {
            lock (timer)
            {
                disposed = true;
                UpdateText(string.Empty);
            }
        }

    }
}

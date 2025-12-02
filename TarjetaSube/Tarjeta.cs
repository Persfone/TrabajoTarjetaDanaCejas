using System;
using System.Linq; //para usar Contains en arrays

namespace TarjetaSube
{
    public class Tarjeta
    {
        private double saldo = 0;
        private double _saldoPendiente = 0;

        public Guid Id { get; }

        private static readonly double[] montosValidos = { 2000, 3000, 4000, 5000, 8000, 10000, 15000, 20000, 25000, 30000 };

        private const double LIMITE_SALDO = 56000;
        const double SALDO_NEGATIVO_MAX = -1200;

        public double Saldo => saldo;
        public double SaldoPendiente => _saldoPendiente;

        //para uso frecuente
        private readonly IClock _clock;
        private int _viajesMes = 0;
        private int _mesRegistrado = 0;
        private int _añoRegistrado = 0;

        //para transbordo - MODIFICADO
        public DateTime? primerViajeTrasbordo = null; // Cambiado: Guarda el primer viaje
        public DateTime? ultimoViajeTrasbordo = null; // Mantenido: Último viaje para actualizar línea
        public string? ultimaLineaTrasbordo = null;

        public Tarjeta(IClock? clock = null)
        {
            this.Id = Guid.NewGuid(); //newGuid es un metodo default para generar un id unico
            _clock = clock ?? new SystemClock();
            DateTime ahora = _clock.Now;
            _mesRegistrado = ahora.Month;
            _añoRegistrado = ahora.Year;
        }

        public bool Cargar(double monto)
        {
            if (!montosValidos.Contains(monto)) return false; //verifica si el monto es valido si no es sale de Cargar y return False


            AcreditarCarga(); //acredita primero cualquier saldo pendiente antes de cargar nuevo saldo

            if (saldo + monto > LIMITE_SALDO)//verifica si al cargar se supera el limite de saldo
            {
                double espacioDisponible = LIMITE_SALDO - saldo;//calcula el espacio disponible hasta el limite
                double montoAcreditable = Math.Min(monto, espacioDisponible);//calcula el monto que se puede acreditar sin superar el limite
                double montoPendiente = monto - montoAcreditable;//calcula el monto que queda pendiente de acreditar

                saldo += montoAcreditable;//acredita el monto que se puede acreditar
                _saldoPendiente += montoPendiente;//agrega el monto pendiente al saldo pendiente
            }
            else
            {
                saldo += monto;//acredita todo el monto si no se supera el limite
            }

            AcreditarCarga();//acredita nuevamente por si se puede acreditar algo del saldo pendiente

            return true;
        }

        public void AcreditarCarga()
        {
            double disponibleParaAcreditar = LIMITE_SALDO - saldo; //56000 - 1777,5 = 54222,5

            if (_saldoPendiente > 0 && disponibleParaAcreditar > 0) //si hay saldo pendiente y hay espacio para acreditar
            {
                //acredita el menor entre lo pendiente y lo que falta para el límite
                double montoAAcreditar = Math.Min(_saldoPendiente, disponibleParaAcreditar); //montoAAcreditar = 1777,5

                saldo += montoAAcreditar; //saldo ahora es 56000
                _saldoPendiente -= montoAAcreditar; //saldoPendiente ahora es 0
            }
        }

        //logica de uso frecuente
        public virtual double ObtenerMontoAPagar(double tarifa)
        {
            if (this.GetType() != typeof(Tarjeta)) //esta linea sirve para determinar si el objeto sobre el q se esta ejecutando el codigo es exactamente de la clase Tarjeta y no de una subclase.                          
            {
                return tarifa; //Si el objeto es de una subclase, se omite la logica de uso frecuente y se devuelve la tarifa sin descuentos
            }


            int proximoViaje = _viajesMes + 1;

            //viajes 1-29: tarifa completa (sin descuento)
            if (proximoViaje >= 1 && proximoViaje <= 29)
                return tarifa;

            //viajes 30-59: 20% de descuento
            else if (proximoViaje >= 30 && proximoViaje <= 59)
                return tarifa * 0.8;

            //viajes 60-80: 25% de descuento
            else if (proximoViaje >= 60 && proximoViaje <= 80)
                return tarifa * 0.75;

            //viaje 81 en adelante: vuelve a tarifa completa
            else
                return tarifa;
        }

        public virtual string ObtenerTipo() => "Tarjeta Normal";


        public virtual bool Pagar(double monto, double tarifaBase = 0)
        {
            //reinicia el contador para uso frecuente dsp del mes
            DateTime ahora = _clock.Now;
            if (ahora.Month != _mesRegistrado || ahora.Year != _añoRegistrado)
            {
                _viajesMes = 0;
                _mesRegistrado = ahora.Month;
                _añoRegistrado = ahora.Year;
            }

            //acreditar primero para usar el saldo disponible al máximo
            AcreditarCarga();
            if (saldo - monto < SALDO_NEGATIVO_MAX) return false;
            saldo -= monto;
            AcreditarCarga();

            if (this.GetType() == typeof(Tarjeta))
            {
                _viajesMes++;
            }

            return true;
        }

        protected static bool EsHoraValidaParaFranquicia(DateTime ahora)
        {
            //solo aplica de Lunes (1) a Viernes (5)
            if (ahora.DayOfWeek == DayOfWeek.Saturday || ahora.DayOfWeek == DayOfWeek.Sunday)
            {
                return false;
            }

            //solo aplica de 6:00 a 22:00
            int hora = ahora.Hour;
            return hora >= 6 && hora < 22;  // De 6:00 inclusive a 22:00 exclusive
        }

        //trasbordo 
        public bool EsTrasbordoValido(DateTime ahora, string lineaActual)
        {
            if (!primerViajeTrasbordo.HasValue || string.IsNullOrEmpty(ultimaLineaTrasbordo))
                return false;

            //lineas distintas
            bool lineaDistinta = !string.Equals(ultimaLineaTrasbordo, lineaActual, StringComparison.OrdinalIgnoreCase); //el ordinalignorecase es para que no importe mayusculas o minusculas y la comparacion
                                                                                                                        // de las letras sea a nivel binario
            if (!lineaDistinta)
                return false;

            //1hs desde el primes viaje (no desde el último)
            TimeSpan diferencia = ahora - primerViajeTrasbordo.Value;
            bool dentroDeHora = diferencia <= TimeSpan.FromHours(1);
            if (!dentroDeHora)
                return false;

            //dia y horario
            bool diaValido = ahora.DayOfWeek >= DayOfWeek.Monday && ahora.DayOfWeek <= DayOfWeek.Saturday;
            bool horarioValido = ahora.Hour >= 7 && ahora.Hour < 22;

            return diaValido && horarioValido; //si las 2 son true => True, en colectivo se cambia esTrasbordo = True
        }

        public void ActualizarTrasbordo(DateTime ahora, string lineaActual)
        {
            // Si es el primer viaje del ciclo de trasbordo, guardamos la hora inicial
            if (!primerViajeTrasbordo.HasValue)
            {
                primerViajeTrasbordo = ahora;
            }

            // Actualizamos siempre el último viaje y la línea (para comparar líneas distintas)
            ultimoViajeTrasbordo = ahora;
            ultimaLineaTrasbordo = lineaActual;

            // Verificamos si ha pasado más de 1 hora desde el primer viaje
            // Si pasó, reiniciamos el contador para el próximo ciclo de trasbordo
            TimeSpan diferencia = ahora - primerViajeTrasbordo.Value;
            if (diferencia > TimeSpan.FromHours(1))
            {
                // Reiniciamos el ciclo de trasbordo
                primerViajeTrasbordo = ahora;
            }
        }
    }

    //-----------------------------------------------------------------------------------//

    public interface IClock
    {
        DateTime Now { get; }
    }

    public class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;
    }
}
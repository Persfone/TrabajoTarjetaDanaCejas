using System;
using System.Linq;

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

        //para transbordo
        protected DateTime? ultimoViajeTrasbordo = null;
        protected string? ultimaLineaTrasbordo = null;

        public Tarjeta(IClock? clock = null)
        {
            this.Id = Guid.NewGuid();
            _clock = clock ?? new SystemClock();
            DateTime ahora = _clock.Now;
            _mesRegistrado = ahora.Month;
            _añoRegistrado = ahora.Year;
        }

        public bool Cargar(double monto)
        {
            if (!montosValidos.Contains(monto)) return false;


            AcreditarCarga();

            if (saldo + monto > LIMITE_SALDO)
            {
                double espacioDisponible = LIMITE_SALDO - saldo;
                double montoAcreditable = Math.Min(monto, espacioDisponible);
                double montoPendiente = monto - montoAcreditable;

                saldo += montoAcreditable;
                _saldoPendiente += montoPendiente;
            }
            else
            {
                saldo += monto;
            }

            AcreditarCarga();

            return true;
        }

        public void AcreditarCarga()
        {
            double disponibleParaAcreditar = LIMITE_SALDO - saldo;

            if (_saldoPendiente > 0 && disponibleParaAcreditar > 0)
            {
                //acredita el menor entre lo pendiente y lo que falta para el límite
                double montoAAcreditar = Math.Min(_saldoPendiente, disponibleParaAcreditar);

                saldo += montoAAcreditar;
                _saldoPendiente -= montoAAcreditar;
            }
        }

        //logica de uso frecuente
        public virtual double ObtenerMontoAPagar(double tarifa)
        {
            if (this.GetType() != typeof(Tarjeta))
            {
                return tarifa;
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
            if (!ultimoViajeTrasbordo.HasValue || string.IsNullOrEmpty(ultimaLineaTrasbordo))
                return false;

            //lineas distintas
            bool lineaDistinta = !string.Equals(ultimaLineaTrasbordo, lineaActual, StringComparison.OrdinalIgnoreCase);
            if (!lineaDistinta)
                return false;

            //1hs
            TimeSpan diferencia = ahora - ultimoViajeTrasbordo.Value;
            bool dentroDeHora = diferencia <= TimeSpan.FromHours(1);
            if (!dentroDeHora)
                return false;

            //dia y horario
            bool diaValido = ahora.DayOfWeek >= DayOfWeek.Monday && ahora.DayOfWeek <= DayOfWeek.Saturday;
            bool horarioValido = ahora.Hour >= 7 && ahora.Hour < 22;

            return diaValido && horarioValido;
        }

        public void ActualizarTrasbordo(DateTime ahora, string lineaActual)
        {
            ultimoViajeTrasbordo = ahora;
            ultimaLineaTrasbordo = lineaActual;
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
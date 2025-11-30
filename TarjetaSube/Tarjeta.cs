using System;
using System.Linq;

namespace TarjetaSube
{
    public class Tarjeta
    {
        private double saldo = 0;
        private double _saldoPendiente = 0;

        // NUEVO: Propiedad ID
        public Guid Id { get; }

        private static readonly double[] montosValidos = { 2000, 3000, 4000, 5000, 8000, 10000, 15000, 20000, 25000, 30000 };

        private const double LIMITE_SALDO = 56000;
        const double SALDO_NEGATIVO_MAX = -1200;

        public double Saldo => saldo;
        public double SaldoPendiente => _saldoPendiente;

        // CAMPOS PARA USO FRECUENTE
        private readonly IClock _clock;
        private int _viajesMes = 0;
        private int _mesRegistrado = 0;
        private int _añoRegistrado = 0;

        // CAMPOS PARA TRASBORDO
        protected DateTime? ultimoViajeTrasbordo = null;
        protected string? ultimaLineaTrasbordo = null;

        // NUEVO: Constructor para inicializar el ID y el reloj
        public Tarjeta(IClock? clock = null)
        {
            this.Id = Guid.NewGuid();
            _clock = clock ?? new SystemClock();
            // Inicializar con el mes y año actual para Uso Frecuente
            DateTime ahora = _clock.Now;
            _mesRegistrado = ahora.Month;
            _añoRegistrado = ahora.Year;
        }

        public bool Cargar(double monto)
        {
            if (!montosValidos.Contains(monto)) return false;

            // PRIMERO: Acreditar lo pendiente que quepa (¡importantísimo!)
            AcreditarCarga();

            // SEGUNDO: Intentar cargar el nuevo monto
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

            // Opcional: acreditar de nuevo por si quedó espacio (no necesario, pero seguro)
            AcreditarCarga();

            return true;
        }

        public void AcreditarCarga()
        {
            double disponibleParaAcreditar = LIMITE_SALDO - saldo;

            if (_saldoPendiente > 0 && disponibleParaAcreditar > 0)
            {
                // Acredita el menor entre lo pendiente y lo que falta para el límite
                double montoAAcreditar = Math.Min(_saldoPendiente, disponibleParaAcreditar);

                saldo += montoAAcreditar;
                _saldoPendiente -= montoAAcreditar;
            }
        }

        // CAMBIO: Lógica de Uso Frecuente movida aquí
        public virtual double ObtenerMontoAPagar(double tarifa)
        {
            // Solo la clase base (Tarjeta Normal) aplica Uso Frecuente
            if (this.GetType() != typeof(Tarjeta))
            {
                return tarifa;
            }

            // Calcular el próximo viaje (viajesMes + 1)
            // NOTA: El contador se actualiza en Pagar
            int proximoViaje = _viajesMes + 1;

            // Viajes 1-29: tarifa completa (sin descuento)
            if (proximoViaje >= 1 && proximoViaje <= 29)
                return tarifa;

            // Viajes 30-59: 20% de descuento
            else if (proximoViaje >= 30 && proximoViaje <= 59)
                return tarifa * 0.8;

            // Viajes 60-80: 25% de descuento
            else if (proximoViaje >= 60 && proximoViaje <= 80)
                return tarifa * 0.75;

            // Viaje 81 en adelante: vuelve a tarifa completa
            else
                return tarifa;
        }

        public virtual string ObtenerTipo() => "Tarjeta Normal";

        // CAMBIO: Se agrega tarifaBase a Pagar (valor por defecto 0 para compatibilidad)
        public virtual bool Pagar(double monto, double tarifaBase = 0)
        {
            // Lógica de Uso Frecuente: Reiniciar contador si es un nuevo mes
            DateTime ahora = _clock.Now;
            if (ahora.Month != _mesRegistrado || ahora.Year != _añoRegistrado)
            {
                _viajesMes = 0;
                _mesRegistrado = ahora.Month;
                _añoRegistrado = ahora.Year;
            }

            // Acreditar primero para usar el saldo disponible al máximo
            AcreditarCarga();
            if (saldo - monto < SALDO_NEGATIVO_MAX) return false;
            saldo -= monto;
            AcreditarCarga();

            // Lógica de Uso Frecuente: Solo incrementar si el pago fue exitoso
            // Y SOLO si es una Tarjeta Normal (no una franquicia)
            if (this.GetType() == typeof(Tarjeta))
            {
                _viajesMes++;
            }

            return true;
        }

        protected static bool EsHoraValidaParaFranquicia(DateTime ahora)
        {
            // Solo aplica de Lunes (1) a Viernes (5)
            if (ahora.DayOfWeek == DayOfWeek.Saturday || ahora.DayOfWeek == DayOfWeek.Sunday)
            {
                return false;
            }

            // Solo aplica de 6:00 a 22:00
            int hora = ahora.Hour;
            return hora >= 6 && hora < 22;  // De 6:00 inclusive a 22:00 exclusive
        }

        // MÉTODO PARA VERIFICAR TRASBORDO
        public bool EsTrasbordoValido(DateTime ahora, string lineaActual)
        {
            if (!ultimoViajeTrasbordo.HasValue || string.IsNullOrEmpty(ultimaLineaTrasbordo))
                return false;

            // Verificar que sea línea distinta
            bool lineaDistinta = !string.Equals(ultimaLineaTrasbordo, lineaActual, StringComparison.OrdinalIgnoreCase);
            if (!lineaDistinta)
                return false;

            // Verificar plazo de 1 hora
            TimeSpan diferencia = ahora - ultimoViajeTrasbordo.Value;
            bool dentroDeHora = diferencia <= TimeSpan.FromHours(1);
            if (!dentroDeHora)
                return false;

            // Verificar días y horario: lunes a sábado de 7:00 a 22:00
            bool diaValido = ahora.DayOfWeek >= DayOfWeek.Monday && ahora.DayOfWeek <= DayOfWeek.Saturday;
            bool horarioValido = ahora.Hour >= 7 && ahora.Hour < 22;

            return diaValido && horarioValido;
        }

        // MÉTODO PARA ACTUALIZAR TRASBORDO
        public void ActualizarTrasbordo(DateTime ahora, string lineaActual)
        {
            ultimoViajeTrasbordo = ahora;
            ultimaLineaTrasbordo = lineaActual;
        }
    }

    //---------------------------INTERFACES Y CLASES DE SISTEMA (Se mantienen)---------------------------------//

    public interface IClock
    {
        DateTime Now { get; }
    }

    public class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;
    }
}
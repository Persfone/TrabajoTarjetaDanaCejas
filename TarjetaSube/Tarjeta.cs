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

        // NUEVO: Constructor para inicializar el ID
        public Tarjeta()
        {
            this.Id = Guid.NewGuid();
        }

        // CAMPOS PARA TRASBORDO
        protected DateTime? ultimoViajeTrasbordo = null;
        protected string? ultimaLineaTrasbordo = null;

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

        public virtual double ObtenerMontoAPagar(double tarifa) => tarifa;
        public virtual string ObtenerTipo() => "Tarjeta Normal";

        public virtual bool Pagar(double monto)
        {
            // Acreditar primero para usar el saldo disponible al máximo
            AcreditarCarga();
            if (saldo - monto < SALDO_NEGATIVO_MAX) return false;
            saldo -= monto;
            AcreditarCarga();
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
        protected bool EsTrasbordoValido(DateTime ahora, string lineaActual)
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
        protected void ActualizarTrasbordo(DateTime ahora, string lineaActual)
        {
            ultimoViajeTrasbordo = ahora;
            ultimaLineaTrasbordo = lineaActual;
        }
    }

    public class MedioBoleto : Tarjeta
    {
        private readonly IClock _clock;
        private DateTime? _ultimoViajeFecha = null;
        private int _viajesHoy = 0;

        public override double ObtenerMontoAPagar(double tarifa) => tarifa / 2.0;
        public override string ObtenerTipo() => "Medio Boleto";

        public MedioBoleto(IClock? clock = null)
        {
            _clock = clock ?? new SystemClock();
        }

        public override bool Pagar(double monto)
        {
            DateTime ahora = _clock.Now;
            DateTime hoy = ahora.Date;

            // Reiniciar contador si es un nuevo día
            if (!_ultimoViajeFecha.HasValue || _ultimoViajeFecha.Value.Date < hoy)
            {
                _viajesHoy = 0;
            }

            // VERIFICACIÓN CORRECTA DEL HORARIO
            bool estaEnHorarioValido = EsHoraValidaParaFranquicia(ahora);
            bool aplicaFranquicia = estaEnHorarioValido && _viajesHoy < 2;

            // CORRECCIÓN: NO dividir por 2 otra vez - el monto YA VIENE con descuento
            // Si NO aplica franquicia, debe pagar tarifa completa (1580)
            double montoADescontar = aplicaFranquicia ? monto : 1580;

            // Verificar intervalo de 5 minutos
            if (_ultimoViajeFecha.HasValue)
            {
                TimeSpan diferencia = ahora - _ultimoViajeFecha.Value;
                if (diferencia < TimeSpan.FromMinutes(5))
                {
                    return false;
                }
            }

            // Intentar pagar
            bool resultado = base.Pagar(montoADescontar);

            // Actualizar contador SOLO si aplicó franquicia
            if (resultado && aplicaFranquicia)
            {
                _viajesHoy++;
            }

            if (resultado)
            {
                _ultimoViajeFecha = ahora;
            }

            return resultado;
        }
    }

    public class BoletoGratuito : Tarjeta
    {
        private readonly IClock _clock;
        private DateTime? _ultimoViajeFecha = null;
        private int _viajesGratisHoy = 0;

        public BoletoGratuito(IClock? clock = null)
        {
            _clock = clock ?? new SystemClock();
        }

        public override double ObtenerMontoAPagar(double tarifa) => tarifa; // ← Siempre devuelve tarifa completa

        public override string ObtenerTipo() => "Boleto Gratuito";

        public override bool Pagar(double monto)
        {
            DateTime ahora = _clock.Now;
            DateTime hoy = ahora.Date;

            // Reiniciar contador si es un nuevo día
            if (!_ultimoViajeFecha.HasValue || _ultimoViajeFecha.Value.Date < hoy)
            {
                _viajesGratisHoy = 0;
            }

            // VERIFICACIÓN CORRECTA DEL HORARIO
            bool estaEnHorarioValido = EsHoraValidaParaFranquicia(ahora);
            bool aplicaFranquicia = estaEnHorarioValido && _viajesGratisHoy < 2;

            // CORRECCIÓN: Si aplica franquicia paga 0, si no paga 1580 (tarifa completa)
            double montoAPagar = aplicaFranquicia ? 0 : 1580;

            // Intentar pagar
            bool pagado = base.Pagar(montoAPagar);
            if (!pagado) return false;

            // Actualizar estado
            _ultimoViajeFecha = ahora;

            if (aplicaFranquicia)
            {
                _viajesGratisHoy++;
            }

            return true;
        }
    }

    public class FranquiciaCompleta : Tarjeta
    {
        public override double ObtenerMontoAPagar(double tarifa) => 0;
        public override bool Pagar(double monto) => true;
        public override string ObtenerTipo() => "Franquicia Completa";
    }

    //---------------------------INTERURBANA---------------------------------//

    public class TarjetaInterurbana : Tarjeta
    {
        public override double ObtenerMontoAPagar(double tarifa) => 3000;
        public override string ObtenerTipo() => "Tarjeta Normal (Interurbana)";
    }

    public class MedioBoletoInterurbano : Tarjeta
    {
        private readonly IClock _clock;
        private DateTime? _ultimoViajeFecha = null;
        private int _viajesHoy = 0;

        public MedioBoletoInterurbano(IClock? clock = null)
        {
            _clock = clock ?? new SystemClock();
        }

        public override double ObtenerMontoAPagar(double tarifa) => 1500;
        public override string ObtenerTipo() => "Medio Boleto (Interurbana)";

        public override bool Pagar(double monto)
        {
            DateTime ahora = _clock.Now;
            DateTime hoy = ahora.Date;

            // Reiniciar contador si es un nuevo día
            if (!_ultimoViajeFecha.HasValue || _ultimoViajeFecha.Value.Date < hoy)
            {
                _viajesHoy = 0;
            }

            // VERIFICACIÓN CORRECTA DEL HORARIO
            bool estaEnHorarioValido = EsHoraValidaParaFranquicia(ahora);
            bool aplicaFranquicia = estaEnHorarioValido && _viajesHoy < 2;

            // CORRECCIÓN: El monto que viene es 1500 (de ObtenerMontoAPagar)
            // Si NO aplica franquicia, debe pagar 3000 (tarifa completa)
            double montoADescontar = aplicaFranquicia ? monto : 3000;

            // Verificar intervalo de 5 minutos
            if (_ultimoViajeFecha.HasValue)
            {
                TimeSpan diferencia = ahora - _ultimoViajeFecha.Value;
                if (diferencia < TimeSpan.FromMinutes(5))
                {
                    return false;
                }
            }

            // Intentar pagar
            bool resultado = base.Pagar(montoADescontar);

            // Actualizar contador SOLO si aplicó franquicia
            if (resultado && aplicaFranquicia)
            {
                _viajesHoy++;
            }

            if (resultado)
            {
                _ultimoViajeFecha = ahora;
            }

            return resultado;
        }
    }

    public class BoletoGratuitoInterurbano : Tarjeta
    {
        private readonly IClock _clock;
        private DateTime? _ultimoViajeFecha = null;
        private int _viajesHoy = 0;

        public BoletoGratuitoInterurbano(IClock? clock = null)
        {
            _clock = clock ?? new SystemClock();
        }

        public override double ObtenerMontoAPagar(double tarifa) => 0;
        public override string ObtenerTipo() => "Boleto Gratuito (Interurbana)";

        public override bool Pagar(double monto)
        {
            DateTime ahora = _clock.Now;
            DateTime hoy = ahora.Date;

            // Reiniciar contador si es un nuevo día
            if (!_ultimoViajeFecha.HasValue || _ultimoViajeFecha.Value.Date < hoy)
            {
                _viajesHoy = 0;
            }

            // VERIFICACIÓN CORRECTA DEL HORARIO
            bool estaEnHorarioValido = EsHoraValidaParaFranquicia(ahora);
            bool aplicaFranquicia = estaEnHorarioValido && _viajesHoy < 2;

            // CORRECCIÓN: Si aplica franquicia paga 0, si no paga 3000
            double montoAPagar = aplicaFranquicia ? 0 : 3000;

            // Intentar pagar
            bool resultado = base.Pagar(montoAPagar);

            if (resultado)
            {
                // Actualizar contador SOLO si aplicó franquicia
                if (aplicaFranquicia)
                {
                    _viajesHoy++;
                }
                _ultimoViajeFecha = ahora;
            }

            return resultado;
        }
    }

    public class FranquiciaCompletaInterurbana : Tarjeta
    {
        public override double ObtenerMontoAPagar(double tarifa) => 0;
        public override bool Pagar(double monto) => true;
        public override string ObtenerTipo() => "Franquicia Completa (Interurbana)";
    }

    //---------------------------USO FRECUENTE---------------------------------//

    public class UsoFrecuente : Tarjeta
    {
        private readonly IClock _clock;
        private int _viajesMes = 0;
        private int _mesRegistrado = 0;
        private int _añoRegistrado = 0;

        public override string ObtenerTipo() => "Tarjeta Uso Frecuente";

        public UsoFrecuente(IClock? clock = null) : base()
        {
            _clock = clock ?? new SystemClock();
            // Inicializar con el mes y año actual
            DateTime ahora = _clock.Now;
            _mesRegistrado = ahora.Month;
            _añoRegistrado = ahora.Year;
        }

        public override double ObtenerMontoAPagar(double tarifa)
        {
            // Calcular el próximo viaje (viajesMes + 1)
            int proximoViaje = _viajesMes + 1;

            // Viajes 1-29: tarifa completa (sin descuento)
            if (proximoViaje >= 1 && proximoViaje <= 29)
                return tarifa; // 1580

            // Viajes 30-59: 20% de descuento
            else if (proximoViaje >= 30 && proximoViaje <= 59)
                return tarifa * 0.8; // 1264

            // Viajes 60-80: 25% de descuento
            else if (proximoViaje >= 60 && proximoViaje <= 80)
                return tarifa * 0.75; // 1185

            // Viaje 81 en adelante: vuelve a tarifa completa
            else
                return tarifa; // 1580
        }

        public override bool Pagar(double monto)
        {
            DateTime ahora = _clock.Now;

            // VERIFICAR SI CAMBIÓ EL MES/AÑO
            if (ahora.Month != _mesRegistrado || ahora.Year != _añoRegistrado)
            {
                _viajesMes = 0;
                _mesRegistrado = ahora.Month;
                _añoRegistrado = ahora.Year;
            }

            // Calcular el monto a pagar según el próximo viaje
            double montoADescontar = ObtenerMontoAPagar(monto);

            // Intentar pagar
            bool pagado = base.Pagar(montoADescontar);

            // Solo incrementar si el pago fue exitoso
            if (pagado)
            {
                _viajesMes++;
            }

            return pagado;
        }
    }

    public interface IClock
    {
        DateTime Now { get; }
    }

    public class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;
    }
}
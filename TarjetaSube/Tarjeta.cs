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
            return hora >= 6 && hora < 22;
        }

    }


    public class MedioBoleto : Tarjeta
    {
        private readonly IClock _clock;
        private DateTime? _ultimoViajeFecha = null;
        private int _viajesHoy = 0;
        // La tarifa a obtener ES la mitad, pero la lógica de cobro está en Pagar()
        public override double ObtenerMontoAPagar(double tarifa) => tarifa / 2.0;
        public override string ObtenerTipo() => "Medio Boleto";

        public MedioBoleto(IClock? clock = null)
        {
            _clock = clock ?? new SystemClock();
        }

        public override bool Pagar(double monto) // monto SIEMPRE es TARIFA_BASICA (1580)
        {
            DateTime ahora = _clock.Now;
            DateTime hoy = ahora.Date;

            // Reiniciar contador si es un nuevo día
            if (!_ultimoViajeFecha.HasValue || _ultimoViajeFecha.Value.Date < hoy)
            {
                _viajesHoy = 0;
            }

            // 1. Determinar si APLICA la FRANQUICIA de Medio Boleto
            bool aplicaFranquicia =
                _viajesHoy < 2 &&
                EsHoraValidaParaFranquicia(ahora);

            double montoADescontar = aplicaFranquicia ? monto / 2.0 : monto;

            // 2. Verificar intervalo de 5 minutos (Aplica para CUALQUIER viaje)
            if (_ultimoViajeFecha.HasValue)
            {
                TimeSpan diferencia = ahora - _ultimoViajeFecha.Value;
                if (diferencia < TimeSpan.FromMinutes(5))
                {
                    return false; // No puede viajar tan rápido.
                }
            }

            // 3. Intentar pagar (tarifa completa o media)
            bool resultado = base.Pagar(montoADescontar);

            // 4. Actualizar el contador y la fecha SOLO si el pago fue exitoso Y fue con franquicia
            if (resultado && aplicaFranquicia)
            {
                _viajesHoy++;
                _ultimoViajeFecha = ahora;
            }
            else if (resultado && !aplicaFranquicia)
            {
                // Si pagó tarifa completa, solo actualiza la fecha (no incrementa viajes de franquicia)
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

        public override bool Pagar(double monto) // monto = 1580
        {
            DateTime ahora = _clock.Now;
            DateTime hoy = ahora.Date;

            // REINICIAR CONTADOR AL CAMBIAR DE DÍA
            if (_ultimoViajeFecha == null || _ultimoViajeFecha.Value.Date < hoy)
            {
                _viajesGratisHoy = 0;
            }

            // ¿Aplica franquicia?
            bool enFranjaHoraria = EsHoraValidaParaFranquicia(ahora);
            bool tieneViajesGratis = _viajesGratisHoy < 2;

            double montoAPagar = (enFranjaHoraria && tieneViajesGratis) ? 0 : monto;

            // Intentar pagar
            bool pagado = base.Pagar(montoAPagar);
            if (!pagado) return false;

            // Actualizar estado SOLO si se pagó
            _ultimoViajeFecha = ahora;

            if (enFranjaHoraria && tieneViajesGratis)
            {
                _viajesGratisHoy++; // solo cuenta si fue gratis
            }

            return true;
        }
    }

    public class FranquiciaCompleta : Tarjeta
    {
        public override double ObtenerMontoAPagar(double tarifa) => 0;
        // Pagar siempre es true porque su saldo negativo máximo es mucho más bajo.
        // Asumo que el requisito era que siempre pueden viajar.
        public override bool Pagar(double monto) => true;
        public override string ObtenerTipo() => "Franquicia Completa";
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
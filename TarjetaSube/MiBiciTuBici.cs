using System;
using System.Collections.Generic; //para diccionarios y listas

namespace TarjetaSube
{
    public class MiBiciTuBici
    {
        public const double TARIFA_DIARIA = 1777.50;
        public const double MULTA_POR_EXCESO = 1000;
        public const double TIEMPO_MAXIMO_USO = 60; // minutos

        private readonly IClock clock;
        private Dictionary<Guid, DateTime> ultimosRetiros = new Dictionary<Guid, DateTime>(); // almacena la fecha y hora del último retiro por tarjeta

        // private Dictionary<Guid, List<DateTime>> historialMultas = new Dictionary<Guid, List<DateTime>>(); 

        private Dictionary<Guid, int> multasPendientes = new Dictionary<Guid, int>(); // almacena la cantidad de multas pendientes por tarjeta

        public BoletoBici? UltimoBoleto { get; private set; }

        public MiBiciTuBici(IClock? clock = null)
        {
            this.clock = clock ?? new SystemClock();
        }

        public bool PagarCon(Tarjeta tarjeta)
        {
            DateTime ahora = clock.Now;

            // Calcula monto a pagar (tarifa + multas pendientes)
            double montoAPagar = CalcularMontoAPagar(tarjeta);

            // Intentar realizar el pago
            bool pagado = tarjeta.Pagar(montoAPagar);

            if (!pagado)
            {
                UltimoBoleto = null;
                return false; //sale del metodo pagarCon sin ejecutar nada de lo de abajo
            }

            // Limpia las multas pendientes si es que fue exitoso 
            if (multasPendientes.ContainsKey(tarjeta.Id))
            {
                multasPendientes.Remove(tarjeta.Id);
            }

            // Retiro exitoso: registra el inicio del nuevo uso.
            RegistrarRetiro(tarjeta, ahora, montoAPagar);

            return true;
        }

        // Se usa para calcular las multas por exceso de tiempo cuando el usuario devuelve la bici.
        public void DevolvioLaBici(Tarjeta tarjeta)
        {
            DateTime ahora = clock.Now;

            if (ultimosRetiros.ContainsKey(tarjeta.Id))
            {
                DateTime ultimoRetiro = ultimosRetiros[tarjeta.Id];

                // Multas calculadas por el tiempo transcurrido
                int multas = CalcularMultasAcumuladas(ultimoRetiro, ahora);

                if (multas > 0)
                {
                    // Almacena solo la cantidad de multas para el próximo cobro.
                    multasPendientes[tarjeta.Id] = multas;

                   
                    /*
                    if (!historialMultas.ContainsKey(tarjeta.Id))
                    {
                        historialMultas[tarjeta.Id] = new List<DateTime>();
                    }
                    historialMultas[tarjeta.Id].Add(ahora);
                    */
                }

                // La bici ya no está "retirada", se elimina de la lista de retiros activos.
                ultimosRetiros.Remove(tarjeta.Id);
            }
        }

        private double CalcularMontoAPagar(Tarjeta tarjeta)
        {
            double montoTotal = TARIFA_DIARIA;

            if (multasPendientes.ContainsKey(tarjeta.Id))
            {
                montoTotal += multasPendientes[tarjeta.Id] * MULTA_POR_EXCESO;
            }

            return montoTotal;
        }

        private int CalcularMultasAcumuladas(DateTime ultimoRetiro, DateTime ahora)
        {
            TimeSpan tiempoTranscurrido = ahora - ultimoRetiro;
            double totalMinutos = tiempoTranscurrido.TotalMinutes;

            // La primera hora (60mins) se cobra por la tarifa default, 
            // la segunda hora adicional es la primera multa.
            // Es decir, si totalMinutos < 120 (2 horas), no hay multa adicional.
            if (totalMinutos < 120)
                return 0;

            // Calcula cuántas horas completas (de 60 minutos) exceden la primera hora.
            int multas = (int)Math.Floor(totalMinutos / 60) - 1; //120/60 = 2 => piso = 2.0 - 1 = 1 multa, 180/60 = 3.0 => piso = 3 - 1 = 2 multas... etc
            return multas;
        }

        private void RegistrarRetiro(Tarjeta tarjeta, DateTime ahora, double montoPagado)
        {
            bool huboMulta = false;
            int multasAplicadas = 0;

            double montoMultas = montoPagado - TARIFA_DIARIA; //en un principio es 0 si no hay multas

            if (montoMultas > 0)
            {
                multasAplicadas = (int)(montoMultas / MULTA_POR_EXCESO); //1000/1000 = 1 => casteo a int = 1, 2000/1000 = 2 => casteo a int = 2... etc
                huboMulta = true;
            }

            // Registra la fecha de retiro para el próximo cálculo de multa.
            ultimosRetiros[tarjeta.Id] = ahora;

            // Creación del boleto
            UltimoBoleto = new BoletoBici(
                idTarjeta: tarjeta.Id,
                fechaHora: ahora,
                tipoTarjeta: tarjeta.ObtenerTipo(),
                montoPagado: montoPagado,
                tarifaBase: TARIFA_DIARIA,
                multasAplicadas: multasAplicadas,
                saldoRestante: tarjeta.Saldo,
                huboMulta: huboMulta
            );
        }
        
        
        // Obtiene multas pendientes
        public int ObtenerMultasPendientes(Tarjeta tarjeta)
        {
            int multas = 0;

            if (multasPendientes.ContainsKey(tarjeta.Id))
            {
                multas += multasPendientes[tarjeta.Id];
            }

            // Calcula multas en curso (si la bici está retirada)
            if (ultimosRetiros.ContainsKey(tarjeta.Id))
            {
                DateTime ultimoRetiro = ultimosRetiros[tarjeta.Id];
                DateTime ahora = clock.Now;
                multas += CalcularMultasAcumuladas(ultimoRetiro, ahora);
            }

            return multas;
        }

        /*
        public List<DateTime> ObtenerHistorialMultas(Tarjeta tarjeta)
        {
            if (historialMultas.ContainsKey(tarjeta.Id))
            {
                return new List<DateTime>(historialMultas[tarjeta.Id]);
            }
            return new List<DateTime>();
        }
        */
    }
}

//La estación de bicicletas debe validar al usuario (la tarjeta),
//pero también debe gestionar y validar el recurso
//(la bicicleta) (no respete la S de solid), por lo que necesita un mecanismo para distinguir
//entre todas las tarjetas que existen
using System;
using System.Collections.Generic;

namespace TarjetaSube
{
    public class BoletoBici
    {
        public Guid IdTarjeta { get; }
        public DateTime FechaHora { get; }
        public string TipoTarjeta { get; }
        public double MontoPagado { get; }
        public double TarifaBase { get; }
        public int MultasAplicadas { get; }
        public double SaldoRestante { get; }
        public bool HuboMulta { get; }

        public BoletoBici(Guid idTarjeta, DateTime fechaHora, string tipoTarjeta,
                         double montoPagado, double tarifaBase, int multasAplicadas,
                         double saldoRestante, bool huboMulta)
        {
            IdTarjeta = idTarjeta;
            FechaHora = fechaHora;
            TipoTarjeta = tipoTarjeta;
            MontoPagado = montoPagado;
            TarifaBase = tarifaBase;
            MultasAplicadas = multasAplicadas;
            SaldoRestante = saldoRestante;
            HuboMulta = huboMulta;
        }
    }
}
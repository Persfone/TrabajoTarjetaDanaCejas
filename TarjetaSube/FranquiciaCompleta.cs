namespace TarjetaSube
{
    public class FranquiciaCompleta : Tarjeta
    {
        public override double ObtenerMontoAPagar(double tarifa) => 0;

        // CAMBIO: Se agrega tarifaBase para coincidir con el método de la clase base
        public override bool Pagar(double monto, double tarifaBase = 0) => true;

        public override string ObtenerTipo() => "Franquicia Completa";
    }
}
namespace TarjetaSube
{
    public class FranquiciaCompleta : Tarjeta
    {
        public override double ObtenerMontoAPagar(double tarifa) => 0;
        public override bool Pagar(double monto) => true;
        public override string ObtenerTipo() => "Franquicia Completa";
    }
}
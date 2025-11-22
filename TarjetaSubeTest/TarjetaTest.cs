using NUnit.Framework;
using TarjetaSube;

[TestFixture]
public class TarjetaTests
{
    private Tarjeta tarjeta;

    [SetUp]
    public void Setup()
    {
        tarjeta = new Tarjeta();
    }

    [Test]
    public void CargarMontoValido_AumentaSaldo()
    {
        bool resultado = tarjeta.Cargar(10000);
        Assert.IsTrue(resultado);
        Assert.AreEqual(10000, tarjeta.Saldo);
    }

    [Test]
    public void CargarMontoNoPermitido_NoCarga()
    {
        bool resultado = tarjeta.Cargar(5000.50);
        Assert.IsFalse(resultado);
        Assert.AreEqual(0, tarjeta.Saldo);
    }

    [Test]
    public void CargarVariasVeces_RespetaLimite40000()
    {
        tarjeta.Cargar(20000);
        tarjeta.Cargar(20000);
        bool resultado = tarjeta.Cargar(100);
        Assert.IsFalse(resultado);
        Assert.AreEqual(40000, tarjeta.Saldo);

        bool excede = tarjeta.Cargar(20000);
        Assert.IsFalse(excede);
        Assert.AreEqual(40000, tarjeta.Saldo);
    }

    [Test]
    public void PagarConSaldoSuficiente_EmiteBoletoYDescuenta()
    {
        tarjeta.Cargar(10000);
        var colectivo = new Colectivo("142 Negra");

        bool ok = colectivo.PagarCon(tarjeta, out Boleto? boleto);

        Assert.IsTrue(ok);
        Assert.IsNotNull(boleto);
        Assert.AreEqual("142 Negra", boleto.Linea);
        Assert.AreEqual(10000 - 1580, boleto.SaldoRestante);
        Assert.AreEqual(10000 - 1580, tarjeta.Saldo);
    }

    [Test]
    public void PagarSinSaldo_NoEmiteBoleto()
    {
        var colectivo = new Colectivo("133");

        bool ok = colectivo.PagarCon(tarjeta, out Boleto? boleto);

        Assert.IsFalse(ok);
        Assert.IsNull(boleto);
        Assert.AreEqual(0, tarjeta.Saldo);
    }

    [Test]
    public void PagarJustoElSaldoExacto_Funciona()
    {
        tarjeta.Cargar(2000);
        tarjeta.Pagar(420); // 1580 exacto

        var colectivo = new Colectivo("153");

        bool ok = colectivo.PagarCon(tarjeta, out Boleto? boleto);

        Assert.IsTrue(ok);
        Assert.IsNotNull(boleto);
        Assert.AreEqual(0, tarjeta.Saldo);
        Assert.AreEqual(0, boleto.SaldoRestante);
    }

    [Test]
    public void MuchasCargasValidas_NoSupera40000()
    {
        var montos = new double[] { 10000, 10000, 10000, 10000 };
        foreach (var m in montos)
            tarjeta.Cargar(m);

        Assert.AreEqual(40000, tarjeta.Saldo);

        bool noPuedoMas = tarjeta.Cargar(2000);
        Assert.IsFalse(noPuedoMas);
        Assert.AreEqual(40000, tarjeta.Saldo);
    }

    [Test]
    public void IntentarCargar0_NoCarga()
    {
        bool resultado = tarjeta.Cargar(0);
        Assert.IsFalse(resultado);
        Assert.AreEqual(0, tarjeta.Saldo);
    }

    [Test]
    public void IntentarCargarNegativo_NoCarga()
    {
        bool resultado = tarjeta.Cargar(-1000);
        Assert.IsFalse(resultado);
        Assert.AreEqual(0, tarjeta.Saldo);
    }

    [Test]
    public void CargarTodosLosMontosValidos_DebeAceptarTodos()
    {
        double[] montos = { 2000, 3000, 4000, 5000, 8000, 10000, 15000, 20000, 25000, 30000 };

        foreach (double m in montos)
        {
            Tarjeta t = new Tarjeta();
            Assert.IsTrue(t.Cargar(m), $"Falla con el monto {m}");
            Assert.AreEqual(m, t.Saldo);
        }
    }

    [Test]
    public void FlujoCompleto_CargarPagarPagarEmitirBoletos()
    {
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(5000);

        var colectivo = new Colectivo("123 Roja");

        bool ok1 = colectivo.PagarCon(tarjeta, out Boleto? b1);
        Assert.IsTrue(ok1);
    }

}
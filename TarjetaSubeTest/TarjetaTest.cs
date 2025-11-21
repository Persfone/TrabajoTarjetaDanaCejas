using NUnit.Framework;
using System;
using System.Linq;

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
        bool resultado = tarjeta.Cargar(5000.50); // no está en la lista
        Assert.IsFalse(resultado);
        Assert.AreEqual(0, tarjeta.Saldo);
    }

    [Test]
    public void CargarVariasVeces_RespetaLimite40000()
    {
        tarjeta.Cargar(20000);
        tarjeta.Cargar(20000);
        bool resultado = tarjeta.Cargar(100); // inválido
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
        var colectivo = new TarjetaSube.Colectivo("142 Negra");

        var boleto = colectivo.PagarCon(tarjeta);

        Assert.IsNotNull(boleto);
        Assert.AreEqual("142 Negra", boleto.Linea);
        Assert.AreEqual(10000 - 1580, boleto.SaldoRestante);
        Assert.AreEqual(10000 - 1580, tarjeta.Saldo);
    }

    [Test]
    public void PagarSinSaldo_NoEmiteBoleto_DevuelveNull()
    {
        var colectivo = new TarjetaSube.Colectivo("133");

        var boleto = colectivo.PagarCon(tarjeta);

        Assert.IsNull(boleto);
        Assert.AreEqual(0, tarjeta.Saldo);
    }

    [Test]
    public void PagarJustoElSaldoExacto_Funciona()
    {
        // ✔ No se puede cargar 1580 → dejamos saldo exacto usando cargas válidas
        tarjeta.Cargar(2000);
        tarjeta.Pagar(420);  // 2000 - 420 = 1580 EXACTO

        var colectivo = new TarjetaSube.Colectivo("153");

        var boleto = colectivo.PagarCon(tarjeta);

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
    public void IntentarCargar0_NoEstaEnLista_NoCarga()
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
}

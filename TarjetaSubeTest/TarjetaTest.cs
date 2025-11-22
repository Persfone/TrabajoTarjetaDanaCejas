using NUnit.Framework;
using System;
using System.Linq;
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
        tarjeta.Cargar(5000); // suficiente para dos viajes

        var colectivo = new TarjetaSube.Colectivo("123 Roja");

        var b1 = colectivo.PagarCon(tarjeta);
        Assert.IsNotNull(b1); // primer viaje

        var b2 = colectivo.PagarCon(tarjeta);
        Assert.IsNotNull(b2); // segundo viaje

        Assert.AreEqual(5000 - 1580 * 2, tarjeta.Saldo);
    }

    [Test]
<<<<<<< HEAD
    public void FranquiciaCompletaSiemprePaga()
    {
        var tarjeta = new FranquiciaCompleta();
        var colectivo = new Colectivo("122");

        Boleto boleto1 = colectivo.PagarCon(tarjeta);
        Boleto boleto2 = colectivo.PagarCon(tarjeta);
        Boleto boleto3 = colectivo.PagarCon(tarjeta);

        Assert.IsNotNull(boleto1);
        Assert.IsNotNull(boleto2);
        Assert.IsNotNull(boleto3);
    }


    [Test]
    public void MedioBoletoPagaLaMitad()
    {
        var tarjeta = new MedioBoleto();
        var colectivo = new Colectivo("122");

        tarjeta.Cargar(2000); // saldo suficiente
        double esperado = Colectivo.TARIFA_BASICA / 2;

        double saldoAntes = tarjeta.Saldo;

        Boleto boleto = colectivo.PagarCon(tarjeta);

        Assert.IsNotNull(boleto);  // pago exitoso

        Assert.AreEqual(saldoAntes - esperado, tarjeta.Saldo);
    }

    public void NoDebePermitirSaldoMenorA1200Negativo()
    {
        Tarjeta t = new Tarjeta();

        // La tarifa es 1580
        bool pago1 = t.Pagar(1580); // saldo = -1580 → NO debe permitirlo
                                    // pero debe permitir hasta -1200

        Assert.IsFalse(pago1);
    }

    [Test]
    public void DebePermitirLlegarAlLimiteNegativo()
    {
        Tarjeta t = new Tarjeta();

        // Permite hasta -1200
        bool pago1 = t.Pagar(1200); // saldo = -1200 → válido
        Assert.IsTrue(pago1);

        // Esto ya no debe permitirlo
        bool pago2 = t.Pagar(1); // -1201 → inválido
        Assert.IsFalse(pago2);
    }

    [Test]
    public void DebeDescontarViajePlusAlCargar()
    {
        Tarjeta t = new Tarjeta();

        // Queda en saldo negativo
        bool pago1 = t.Pagar(1200); // saldo = -1200
        Assert.IsTrue(pago1);

        // Ahora carga dinero
        bool carga = t.Cargar(2000);
        Assert.IsTrue(carga);

        // Debe haber descontado automáticamente el saldo negativo
        Assert.AreEqual(800, t.Saldo);
    }

}

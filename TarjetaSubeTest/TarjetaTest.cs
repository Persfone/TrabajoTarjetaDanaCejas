using NUnit.Framework;
using System;
using TarjetaSube;

[TestFixture]
public class TarjetaTests
{
    private static readonly double[] MontosValidos = { 2000, 3000, 4000, 5000, 8000, 10000, 15000, 20000, 25000, 30000 };
    private const double SALDO_NEGATIVO_MAX = -1200;
    private const double TARIFA_BASICA = 1580;
    private Colectivo colectivo;

    [SetUp]
    public void Setup()
    {
        colectivo = new Colectivo("144 Roja");
    }

    private class FakeClock : IClock
    {
        public DateTime Now { get; set; }
        public FakeClock(DateTime initialTime) => Now = initialTime;
        public void AdvanceMinutes(int minutes) => Now = Now.AddMinutes(minutes);
    }

    [Test]
    public void Cargar_TodosLosMontosValidos_AceptaYActualizaSaldoCorrectamente()
    {
        var tarjeta = new Tarjeta();
        foreach (var monto in MontosValidos)
        {
            Assert.That(tarjeta.Cargar(monto), Is.True, $"Debería aceptar cargar {monto}");
            Assert.That(tarjeta.Saldo, Is.EqualTo(monto).Within(0.01));
            tarjeta = new Tarjeta();
        }
    }

    [Test]
    public void Cargar_MontoInvalido_NoModificaSaldoYRetornaFalse()
    {
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(10000);
        Assert.That(tarjeta.Cargar(1234), Is.False);
        Assert.That(tarjeta.Saldo, Is.EqualTo(10000).Within(0.01));
    }

    [Test]
    public void Pagar_SaldoNoPuedeQuedarPorDebajoDeNegativoMaximo()
    {
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(2000);
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(colectivo.PagarCon(tarjeta), Is.False);
        Assert.That(tarjeta.Saldo, Is.EqualTo(-1160).Within(0.01));
    }

    [Test]
    public void Pagar_TarjetaNormal_DesuentaTarifaCompletaCorrectamente()
    {
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(10000);
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(10000 - 1580).Within(0.01));
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(10000 - 3160).Within(0.01));
    }

    [Test]
    public void Pagar_FranquiciaCompleta_SiemprePermiteViajeSinDescontar()
    {
        var tarjeta = new FranquiciaCompleta();
        tarjeta.Cargar(10000);
        for (int i = 0; i < 100; i++)
            Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(10000).Within(0.01));
    }

    [Test]
    public void Colectivo_PagarCon_RespetaTipoDeTarjeta_CorrectoDescuento()
    {
        var normal = new Tarjeta(); normal.Cargar(10000);
        var medio = new MedioBoleto(); medio.Cargar(10000);
        var gratis = new BoletoGratuito();
        var franquicia = new FranquiciaCompleta();

        Assert.That(colectivo.PagarCon(normal), Is.True);
        Assert.That(normal.Saldo, Is.EqualTo(10000 - 1580).Within(0.01));
        Assert.That(colectivo.PagarCon(medio), Is.True);
        Assert.That(medio.Saldo, Is.EqualTo(10000 - 790).Within(0.01));
        Assert.That(colectivo.PagarCon(gratis), Is.True);
        Assert.That(colectivo.PagarCon(franquicia), Is.True);
    }

    [Test]
    public void MedioBoleto_RestriccionDe5Minutoso()
    {
        var clock = new FakeClock(new DateTime(2025, 5, 1, 9, 0, 0));
        var tarjeta = new MedioBoleto(clock);
        var colectivoLocal = new Colectivo("144");

        tarjeta.Cargar(2000);
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(1210).Within(0.01));

        clock.AdvanceMinutes(4);
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.False);

        clock.AdvanceMinutes(1);
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(420).Within(0.01));
    }

    [Test]
    public void MedioBoleto_MaximoDosViajesConDescuentoPorDia_TercerViajeCobraCompleto()
    {
        var clock = new FakeClock(new DateTime(2025, 6, 10, 8, 0, 0));
        var tarjeta = new MedioBoleto(clock);
        var colectivoLocal = new Colectivo("60");
        tarjeta.Cargar(5000);
        double saldoEsperado = 5000;

        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        saldoEsperado -= 790;
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01));

        clock.AdvanceMinutes(6);
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        saldoEsperado -= 790;
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01));

        clock.AdvanceMinutes(6);
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        saldoEsperado -= 1580;
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01));
    }

    [Test]
    public void BoletoGratuito_NoMasDeDosGratisPorDia_ConClockControlado()
    {
        var clock = new FakeClock(new DateTime(2025, 4, 5, 10, 0, 0));
        var tarjeta = new BoletoGratuito(clock);
        tarjeta.Cargar(2000);

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(2000).Within(0.01));

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(420).Within(0.01));
    }

    [Test]
    public void BoletoGratuito_SeReiniciaAlDiaSiguiente_ConClock()
    {
        var clock = new FakeClock(new DateTime(2025, 4, 5));
        var tarjeta = new BoletoGratuito(clock);
        var colectivoLocal = new Colectivo("60", clock);
        tarjeta.Cargar(5000);

        colectivoLocal.PagarCon(tarjeta);
        colectivoLocal.PagarCon(tarjeta);
        colectivoLocal.PagarCon(tarjeta);
        Assert.That(tarjeta.Saldo, Is.EqualTo(5000 - 1580).Within(0.01));

        clock.Now = new DateTime(2025, 4, 6);
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(5000 - 1580).Within(0.01));
    }

    [Test]
    public void Cargar_ExcedenteQuedaPendiente_SiSuperaElLimite()
    {
        var tarjeta = new Tarjeta();
        Assert.That(tarjeta.Cargar(30000), Is.True);
        Assert.That(tarjeta.Cargar(30000), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(56000).Within(0.01));
        Assert.That(tarjeta.SaldoPendiente, Is.EqualTo(4000).Within(0.01));
    }

    [Test]
    public void Pagar_AcreditaSaldoPendiente_DespuesDeCadaViaje()
    {
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(30000);
        tarjeta.Cargar(30000);
        Assert.That(tarjeta.Saldo, Is.EqualTo(56000));
        Assert.That(tarjeta.SaldoPendiente, Is.EqualTo(4000));

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(56000));
        Assert.That(tarjeta.SaldoPendiente, Is.EqualTo(2420));

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(56000));
        Assert.That(tarjeta.SaldoPendiente, Is.EqualTo(840));
    }

    [Test]
    public void Colectivo_EmiteBoletoCorrectoParaCadaTipoDeTarjeta_YDevuelveTrue()
    {
        var clock = new FakeClock(new DateTime(2025, 6, 6, 18, 30, 0));
        var colectivoLocal = new Colectivo("144", clock);

        var normal = new Tarjeta(); normal.Cargar(10000);
        var medio = new MedioBoleto(clock); medio.Cargar(10000);
        var gratuito = new BoletoGratuito(clock); gratuito.Cargar(5000);
        var franquicia = new FranquiciaCompleta();

        Assert.That(colectivoLocal.PagarCon(normal), Is.True);
        Assert.That(colectivoLocal.UltimoBoleto?.TipoTarjeta, Is.EqualTo("Tarjeta Normal"));

        Assert.That(colectivoLocal.PagarCon(gratuito), Is.True);
        Assert.That(colectivoLocal.UltimoBoleto?.TipoTarjeta, Is.EqualTo("Boleto Gratuito"));

        Assert.That(colectivoLocal.PagarCon(franquicia), Is.True);
        Assert.That(colectivoLocal.UltimoBoleto?.TipoTarjeta, Is.EqualTo("Franquicia Completa"));

        clock.AdvanceMinutes(6);
        Assert.That(colectivoLocal.PagarCon(medio), Is.True);
        Assert.That(colectivoLocal.UltimoBoleto?.TipoTarjeta, Is.EqualTo("Medio Boleto"));
        Assert.That(colectivoLocal.UltimoBoleto?.MontoDescontado, Is.EqualTo(790));
    }

    [Test]
    public void Boleto_RecuperaDeudaCorrectamente_CuandoSaldoNegativoPeroPermiteViaje()
    {
        var clock = new FakeClock(new DateTime(2025, 6, 6, 19, 0, 0));
        var colectivoLocal = new Colectivo("60", clock);
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(2000);

        colectivoLocal.PagarCon(tarjeta);
        colectivoLocal.PagarCon(tarjeta);

        var boleto = colectivoLocal.UltimoBoleto;
        Assert.That(boleto?.SaldoRestante, Is.EqualTo(-1160).Within(0.01));
        Assert.That(boleto?.MontoTotalAbonado, Is.EqualTo(1580));
    }

    [Test]
    public void Trasbordo_Gratuito_CuandoCumpleCondiciones()
    {
        var clock = new FakeClock(new DateTime(2025, 4, 5, 10, 0, 0));
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(10000);

        var colectivoA = new Colectivo("144 Roja", clock);
        var colectivoB = new Colectivo("144 Negra", clock);

        Assert.That(colectivoA.PagarCon(tarjeta), Is.True);
        Assert.That(colectivoA.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580));
        Assert.That(colectivoA.UltimoBoleto?.EsTrasbordo, Is.False);

        clock.AdvanceMinutes(20);

        Assert.That(colectivoB.PagarCon(tarjeta), Is.True);
        Assert.That(colectivoB.UltimoBoleto?.MontoDescontado, Is.EqualTo(0));
        Assert.That(colectivoB.UltimoBoleto?.EsTrasbordo, Is.True);
    }

    [Test]
    public void Trasbordo_NoGratuito_SiMismaLinea()
    {
        var clock = new FakeClock(new DateTime(2025, 4, 5, 10, 0, 0));
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(10000);
        var colectivoLocal = new Colectivo("144 Roja", clock);

        colectivoLocal.PagarCon(tarjeta);
        clock.AdvanceMinutes(10);
        colectivoLocal.PagarCon(tarjeta);

        Assert.That(colectivoLocal.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580));
        Assert.That(colectivoLocal.UltimoBoleto?.EsTrasbordo, Is.False);
    }
}
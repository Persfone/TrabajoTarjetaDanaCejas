using NUnit.Framework;
using System;
using System.Linq;
using TarjetaSube;

[TestFixture]
public class TarjetaTests
{
    private static readonly double[] MontosValidos = { 2000, 3000, 4000, 5000, 8000, 10000, 15000, 20000, 25000, 30000 };
    private const double LIMITE_SALDO = 40000;
    private const double SALDO_NEGATIVO_MAX = -1200;
    private const double TARIFA_BASICA = 1580;

    private Colectivo colectivo;

    [SetUp]
    public void Setup()
    {
        colectivo = new Colectivo("144 Roja");
    }

    [Test]
    public void Cargar_TodosLosMontosValidos_AceptaYActualizaSaldoCorrectamente()
    {
        var tarjeta = new Tarjeta();

        foreach (var monto in MontosValidos)
        {
            Assert.That(tarjeta.Cargar(monto), Is.True, $"Debería aceptar cargar {monto}");
            Assert.That(tarjeta.Saldo, Is.EqualTo(monto).Within(0.01));
            tarjeta = new Tarjeta(); // reiniciar para el siguiente monto
        }
    }

    [Test]
    public void Cargar_MontoInvalido_NoModificaSaldoYRetornaFalse()
    {
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(10000);

        Assert.That(tarjeta.Cargar(1234), Is.False);
        Assert.That(tarjeta.Saldo, Is.EqualTo(10000).Within(0.01));

        Assert.That(tarjeta.Cargar(99999), Is.False);
        Assert.That(tarjeta.Saldo, Is.EqualTo(10000).Within(0.01));
    }

    [Test]
    public void Cargar_SaldoSuperaLimite_NoPermiteCarga()
    {
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(30000);
        tarjeta.Cargar(10000);

        Assert.That(tarjeta.Cargar(2000), Is.False);
        Assert.That(tarjeta.Saldo, Is.EqualTo(40000).Within(0.01));
    }

    [Test]
    public void Pagar_SaldoNoPuedeQuedarPorDebajoDeNegativoMaximo()
    {
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(2000);

        // Con $2000 → puede hacer 2 viajes completos + varios en negativo
        // 2000 - 1580 = 420
        // 420 - 1580 = -1160 (aceptable)
        // -1160 - 1580 = -2740 → NO permitido

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);  // viaje 1 → saldo = 420
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);  // viaje 2 → saldo = -1160
        Assert.That(colectivo.PagarCon(tarjeta), Is.False); // viaje 3 → RECHAZADO

        Assert.That(tarjeta.Saldo, Is.EqualTo(-1160).Within(0.01));
        Assert.That(tarjeta.Saldo, Is.GreaterThanOrEqualTo(SALDO_NEGATIVO_MAX));
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
    /*
    [Test]
    public void Pagar_MedioBoleto_DesuentaExactamenteLaMitad()
    {
        var tarjeta = new MedioBoleto();
        tarjeta.Cargar(5000);

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(5000 - 790).Within(0.01));

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(5000 - 1580).Within(0.01));
    }
  
    [Test]
    public void Pagar_BoletoGratuito_NoDescuentaNada()
    {
        var tarjeta = new BoletoGratuito();
        tarjeta.Cargar(3000);

        for (int i = 0; i < 50; i++)
        {
            Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        }

        Assert.That(tarjeta.Saldo, Is.EqualTo(3000).Within(0.01));
    }
   */
    [Test]
    public void Pagar_FranquiciaCompleta_SiemprePermiteViajeSinDescontar()
    {
        var tarjeta = new FranquiciaCompleta();
        tarjeta.Cargar(10000);

        for (int i = 0; i < 100; i++)
        {
            Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        }

        Assert.That(tarjeta.Saldo, Is.EqualTo(10000).Within(0.01));
    }

    [Test]
    public void Colectivo_PagarCon_RespetaTipoDeTarjeta_CorrectoDescuento()
    {
        var normal = new Tarjeta();
        var medio = new MedioBoleto();
        var gratis = new BoletoGratuito();
        var franquicia = new FranquiciaCompleta();

        normal.Cargar(10000);
        medio.Cargar(10000);

        Assert.That(colectivo.PagarCon(normal), Is.True);
        Assert.That(normal.Saldo, Is.EqualTo(10000 - 1580).Within(0.01));

        Assert.That(colectivo.PagarCon(medio), Is.True);
        Assert.That(medio.Saldo, Is.EqualTo(10000 - 790).Within(0.01));

        Assert.That(colectivo.PagarCon(gratis), Is.True);
        Assert.That(colectivo.PagarCon(franquicia), Is.True);
    }


    private class FakeClock : IClock
    {
        public DateTime Now { get; set; }
    }

    [Test]
    public void MedioBoleto_RestriccionDe5Minutoso()
    {
        var clock = new FakeClock { Now = new DateTime(2025, 5, 1, 9, 0, 0) };
        var tarjeta = new MedioBoleto(clock);
        var colectivo = new Colectivo("144");

        tarjeta.Cargar(2000);

        // --- VIAJE 1: Exitoso (Medio Boleto) ---
        // Saldo: 2000 - 790 = 1210
        Assert.That(colectivo.PagarCon(tarjeta), Is.True, "El primer viaje debe ser aceptado.");
        Assert.That(tarjeta.Saldo, Is.EqualTo(1210).Within(0.01)); // Saldo esperado 1210

        // --- VIAJE 2: Rechazo por Tiempo (< 5 minutos) ---
        clock.Now = clock.Now.AddMinutes(4);

        Assert.That(colectivo.PagarCon(tarjeta), Is.False, "El pago debe ser rechazado si no pasaron 5 minutos.");
        Assert.That(tarjeta.Saldo, Is.EqualTo(1210).Within(0.01), "El saldo NO debe cambiar tras el rechazo por tiempo.");

        // --- VIAJE 3: Exitoso (Medio Boleto) después de 5 minutos ---
        // Avanzamos el tiempo para que hayan pasado 5 minutos desde el Viaje 1.
        clock.Now = new DateTime(2025, 5, 1, 9, 5, 0);

        // ✅ AJUSTE: Saldo: 1210 - 790 = 420 (Viaje 2 con descuento del día)
        Assert.That(colectivo.PagarCon(tarjeta), Is.True, "El pago debe ser aceptado al pasar los 5 minutos.");
        Assert.That(tarjeta.Saldo, Is.EqualTo(420).Within(0.01)); // Saldo esperado 420
    }

    [Test]
    public void MedioBoleto_MaximoDosViajesConDescuentoPorDia_TercerViajeCobraCompleto()
    {
        var clock = new FakeClock { Now = new DateTime(2025, 6, 10, 8, 0, 0) };
        var tarjeta = new MedioBoleto(clock);
        var colectivo = new Colectivo("60");

        // Cargamos saldo suficiente (5000) para 2 MB + 2 viajes completos sin alcanzar el límite negativo.
        tarjeta.Cargar(5000);
        double saldoEsperado = 5000;

        // --- VIAJE 1: Aplica Descuento (790) ---
        // Saldo: 5000 - 790 = 4210
        Assert.That(colectivo.PagarCon(tarjeta), Is.True, "Viaje 1 debe aplicar medio boleto.");
        saldoEsperado -= 790;
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01));
        clock.Now = clock.Now.AddMinutes(6); // Esperamos > 5 min

        // --- VIAJE 2: Aplica Descuento (790) ---
        // Saldo: 4210 - 790 = 3420
        Assert.That(colectivo.PagarCon(tarjeta), Is.True, "Viaje 2 debe aplicar medio boleto.");
        saldoEsperado -= 790;
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01)); // Saldo: 3420
        clock.Now = clock.Now.AddMinutes(6);

        // --- VIAJE 3: COBRA TARIFA COMPLETA (1580) ---
        // Se agota el límite diario de descuentos.
        // Saldo: 3420 - 1580 = 1840
        Assert.That(colectivo.PagarCon(tarjeta), Is.True, "Viaje 3 debe cobrar la tarifa completa (1580).");
        saldoEsperado -= 1580;
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01)); // Saldo: 1840
        Assert.That(tarjeta.Saldo, Is.Not.EqualTo(1840 - 790).Within(0.01), "Verifica que el saldo NO es el de un medio boleto.");
        clock.Now = clock.Now.AddMinutes(6);

        // --- VIAJE 4: COBRA TARIFA COMPLETA (1580) ---
        // Saldo: 1840 - 1580 = 260
        Assert.That(colectivo.PagarCon(tarjeta), Is.True, "Viaje 4 debe seguir cobrando tarifa completa.");
        saldoEsperado -= 1580;
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01)); // Saldo: 260
    }

    [Test]
        public void BoletoGratuito_NoMasDeDosGratisPorDia_ConClockControlado()
    {
        var clock = new FakeClock { Now = new DateTime(2025, 4, 5, 10, 0, 0) };
        var tarjeta = new BoletoGratuito(clock);
        tarjeta.Cargar(2000);
        // 1° y 2° viaje → gratis
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(2000).Within(0.01)); // Saldo debe ser 2000

        // 3° viaje → cobra $1580. Saldo: 2000 - 1580 = 420
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(420).Within(0.01));

        // 4° viaje → también cobra $1580. Saldo: 420 - 1580 = -1160
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(-1160).Within(0.01)); // Saldo esperado: -1160
    }

    [Test]
    public void BoletoGratuito_SeReiniciaAlDiaSiguiente_ConClock()
    {
        var clock = new FakeClock { Now = new DateTime(2025, 4, 5) };
        var tarjeta = new BoletoGratuito(clock);
        var colectivo = new Colectivo("60");

        tarjeta.Cargar(5000);

        // Día 1: 3 viajes → 2 gratis + 1 cobrado
        colectivo.PagarCon(tarjeta);
        colectivo.PagarCon(tarjeta);
        colectivo.PagarCon(tarjeta); // este cobra. Saldo: 5000 - 1580 = 3420
        Assert.That(tarjeta.Saldo, Is.EqualTo(5000 - 1580).Within(0.01)); // Saldo esperado: 3420

        // Cambiamos al día siguiente
        clock.Now = new DateTime(2025, 4, 6);

        // Día 2: 1er viaje gratis. Saldo: 3420
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(3420).Within(0.01)); // Saldo debe mantenerse

        // Día 2: 2do viaje gratis. Saldo: 3420
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(3420).Within(0.01));

        // Día 2: 3er viaje → cobra $1580. Saldo: 3420 - 1580 = 1840
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(3420 - 1580).Within(0.01)); // Saldo esperado: 1840
    }
}

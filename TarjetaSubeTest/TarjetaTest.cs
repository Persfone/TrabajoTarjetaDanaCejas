using NUnit.Framework;
using System;
using System.Linq;
using TarjetaSube;

[TestFixture]
public class TarjetaTests
{
    private static readonly double[] MontosValidos = { 2000, 3000, 4000, 5000, 8000, 10000, 15000, 20000, 25000, 30000 };
    private const double SALDO_NEGATIVO_MAX = -1200;
    private const double TARIFA_BASICA = 1580;
    private const double TARIFA_NORMAL = 1580.0;     // Viajes 1-29 y 81+
    private const double TARIFA_20_DTO = 1264.0;    // 1580 * 0.8 = 1264.0 (Viajes 30-59)
    private const double TARIFA_25_DTO = 1185.0;

    private Colectivo colectivo;
    private Colectivo colectivoInterurbano;
    private FakeClock clock;

    [SetUp]
    public void Setup()
    {
        colectivo = new Colectivo("144 Roja");
        colectivoInterurbano = new Colectivo("Línea 500 (Interurbana)");
        clock = new FakeClock { Now = new DateTime(2024, 1, 1, 10, 0, 0) };
    }

    private class FakeClock : IClock
    {
        public DateTime Now { get; set; }
        public FakeClock() { }
        public FakeClock(DateTime initialTime) => Now = initialTime;
        public void AdvanceMinutes(int minutes) => Now = Now.AddMinutes(minutes);
        public void AdvanceDays(int days) => Now = Now.AddDays(days);
        public void AdvanceMonths(int months) => Now = Now.AddMonths(months);
    }

    // ==================== TESTS BÁSICOS DE CARGA Y PAGO ====================
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
    public void Cargar_MontoNoValido_RechazaYNoActualizaSaldo()
    {
        var tarjeta = new Tarjeta();
        Assert.That(tarjeta.Cargar(100), Is.False);
        Assert.That(tarjeta.Cargar(2100), Is.False);
        Assert.That(tarjeta.Saldo, Is.EqualTo(0));
    }

    [Test]
    public void Cargar_AcumulaSaldo()
    {
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(2000);
        tarjeta.Cargar(3000);
        Assert.That(tarjeta.Saldo, Is.EqualTo(5000));
    }

    [Test]
    public void Pagar_Con2000_PermiteDosViajes_HastaCasi1200Negativo()
    {
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(2000);

        Assert.That(colectivo.PagarCon(tarjeta), Is.True); // 2000 → 420
        Assert.That(colectivo.PagarCon(tarjeta), Is.True); // 420 → -1160

        Assert.That(tarjeta.Saldo, Is.EqualTo(-1160).Within(0.01));
        Assert.That(colectivo.PagarCon(tarjeta), Is.False); // -1160 -1580 = -2740 → excede -1200
        Assert.That(tarjeta.Saldo, Is.EqualTo(-1160).Within(0.01)); // no cambió
    }

    [Test]
    public void Cargar_ConCreditoPendiente_AcreditaTodoPendienteYRestoASaldo()
    {
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(30000);
        tarjeta.Cargar(30000); // 56000 + 4000 pendiente

        Assert.That(tarjeta.Saldo, Is.EqualTo(56000));
        Assert.That(tarjeta.SaldoPendiente, Is.EqualTo(4000));

        colectivo.PagarCon(tarjeta); // 56000 → 54420 → acredita 1580 del pendiente

        // Ahora: saldo = 56000, pendiente = 2420

        tarjeta.Cargar(2000); // ← todo va a pendiente (no hay espacio)

        Assert.That(tarjeta.Saldo, Is.EqualTo(56000));
        Assert.That(tarjeta.SaldoPendiente, Is.EqualTo(4420).Within(0.01)); // 4420
    }

    [Test]
    public void Descuenta_Bien()
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

    // ==================== TESTS DE FRANQUICIAS URBANAS ====================
    [Test]
    public void MedioBoleto_RestriccionDe5Minutos()
    {
        var clock = new FakeClock(new DateTime(2025, 5, 1, 9, 0, 0));
        var tarjeta = new MedioBoleto(clock);
        var colectivoLocal = new Colectivo("144", clock);

        tarjeta.Cargar(2000);

        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(1210).Within(0.01));

        clock.AdvanceMinutes(4);
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.False);
        Assert.That(tarjeta.Saldo, Is.EqualTo(1210).Within(0.01));

        clock.AdvanceMinutes(1);
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(420).Within(0.01));
    }
    //-------------------------------------------TEST PAGAR CON 2 DE ITERACION 5 -----------------------------//
    [Test]
    public void MedioBoleto_MaximoDosViajesConDescuentoPorDia_TercerViajeCobraCompleto()
    {
        var clock = new FakeClock(new DateTime(2025, 6, 10, 8, 0, 0));
        var tarjeta = new MedioBoleto(clock);
        var colectivoLocal = new Colectivo("60", clock);
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

        clock.AdvanceMinutes(6);
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        saldoEsperado -= 1580;
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01));
    }
    //------------------------ TEST PAGAR CON 1 ITERACION 5 -----------------------------//
    [Test]
    public void MedioBoleto_MaximoDosViajesConDescuentoPorDia_ConSaldoNegativo()
    {
        var clock = new FakeClock(new DateTime(2025, 6, 10, 8, 0, 0));
        var tarjeta = new MedioBoleto(clock);
        var colectivoLocal = new Colectivo("60", clock);

        // CARGA 2000
        tarjeta.Cargar(2000);
        double saldoEsperado = 2000;

        // PAGA 2 VIAJES CADA 6 MINUTOS PARA Q LOS COBRE Y SEA MEDIO BOLETO
        // Primer viaje con medio boleto (790)
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True, "Primer viaje debería ser exitoso con medio boleto");
        saldoEsperado -= 790;
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01), $"Después del primer viaje, saldo debería ser {saldoEsperado}");//-1210

        clock.AdvanceMinutes(3); // Solo pasaron 3 minutos
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.False, "No debería poder pagar antes de los 5 minutos");
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01), "El saldo no debería cambiar cuando el pago es rechazado por tiempo");//0

        // Segundo viaje con medio boleto (790) - aún en el límite de 2 viajes por día
        clock.AdvanceMinutes(6);
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True, "Segundo viaje debería ser exitoso con medio boleto");
        saldoEsperado -= 790;
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01), $"Después del segundo viaje, saldo debería ser {saldoEsperado}"); //420

        // PASA 1 DÍA (SE REINICIA EL CONTADOR DE MEDIO BOLETO)
        clock.AdvanceDays(1);

        // VUELVE A PAGAR 1 VIAJE - debería aplicar medio boleto nuevamente
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True, "Cuarto viaje (nuevo día) debería ser exitoso con medio boleto");
        saldoEsperado -= 790;
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01), $"Después del cuarto viaje (nuevo día), saldo debería ser {saldoEsperado} (medio boleto reiniciado)"); // -370

        clock.AdvanceMinutes(6);
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True, "Quinto viaje (nuevo día) debería ser exitoso con medio boleto");
        saldoEsperado -= 790;
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01), $"Después del cuarto viaje (nuevo día), saldo debería ser {saldoEsperado}"); //-1160

        clock.AdvanceMinutes(6);
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.False, "Cuarto viaje debería fallar por exceso de saldo negativo");
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01), "El saldo no debería cambiar cuando el pago es rechazado por saldo insuficiente"); //-1950
        

    }

    [Test]
    public void BoletoGratuito_NoMasDeDosGratisPorDia_ConClockControlado()
    {
        var clock = new FakeClock(new DateTime(2025, 4, 5, 7, 0, 0));
        var tarjeta = new BoletoGratuito(clock);
        tarjeta.Cargar(2000);
        var colectivoControlado = new Colectivo("102", clock);

        colectivoControlado.PagarCon(tarjeta); // Saldo 2000. ViajesHoy 1.
        clock.AdvanceMinutes(10);
        colectivoControlado.PagarCon(tarjeta); // Saldo 2000. ViajesHoy 2.
        clock.AdvanceMinutes(10);
        colectivoControlado.PagarCon(tarjeta); // Saldo 2000 - 1580 = 420. ViajesHoy 2.
        clock.AdvanceMinutes(10);
        colectivoControlado.PagarCon(tarjeta); // Saldo 420 - 1580 = -1160. ViajesHoy 2.

        Assert.That(tarjeta.Saldo, Is.EqualTo(-1160).Within(0.01));
    }

    [Test]
    public void BoletoGratuito_SeReiniciaAlDiaSiguiente_ConClock()
    {
        var clock = new FakeClock(new DateTime(2025, 11, 24, 10, 0, 0)); // Lunes
        var colectivoLocal = new Colectivo("102", clock);
        var tarjeta = new BoletoGratuito(clock);
        tarjeta.Cargar(10000);

        // Día 1: 2 gratis + 1 pago
        colectivoLocal.PagarCon(tarjeta); // gratis
        colectivoLocal.PagarCon(tarjeta); // gratis
        colectivoLocal.PagarCon(tarjeta); // paga → saldo = 10000 - 1580 = 8420

        // Cambiar al día siguiente
        clock.Now = new DateTime(2025, 11, 25, 10, 0, 0); // Martes

        // Día 2: vuelve a tener 2 viajes gratis
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(8420)); // no bajó

        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(8420)); // sigue igual

        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(8420 - 1580).Within(0.01)); // ahora sí paga
    }

    // ==================== TESTS DE HORARIO DE FRANQUICIAS ====================
    [Test]
    public void Franquicias_FueraDeHorario_NoAplicanBeneficio()
    {
        var clock = new FakeClock(new DateTime(2025, 11, 24, 23, 30, 0)); // Lunes 23:30
        var colectivoLocal = new Colectivo("144", clock);

        var medio = new MedioBoleto(clock);
        var gratuito = new BoletoGratuito(clock);

        medio.Cargar(10000);
        gratuito.Cargar(10000);

        Assert.That(colectivoLocal.PagarCon(medio), Is.True);
        Assert.That(medio.Saldo, Is.EqualTo(10000 - 1580).Within(0.01)); // pagó completo!

        Assert.That(colectivoLocal.PagarCon(gratuito), Is.True);
        Assert.That(gratuito.Saldo, Is.EqualTo(10000 - 1580).Within(0.01)); // pagó completo!
    }

    [Test]
    public void Franquicias_EnHorario_SiAplicanBeneficio()
    {
        var clock = new FakeClock(new DateTime(2025, 11, 24, 14, 0, 0)); // Lunes 14hs
        var colectivoLocal = new Colectivo("60", clock);

        var medio = new MedioBoleto(clock);
        var gratuito = new BoletoGratuito(clock);

        medio.Cargar(10000);
        gratuito.Cargar(10000);

        Assert.That(colectivoLocal.PagarCon(medio), Is.True);
        Assert.That(medio.Saldo, Is.EqualTo(10000 - 790).Within(0.01)); // 50%

        Assert.That(colectivoLocal.PagarCon(gratuito), Is.True);
        Assert.That(gratuito.Saldo, Is.EqualTo(10000)); // gratis
    }

    // ==================== TESTS DE FRANQUICIAS INTERURBANAS ====================
    [Test]
    public void BoletoGratuitoInterurbano_SeReiniciaAlDiaSiguiente()
    {
        var tarjeta = new BoletoGratuitoInterurbano(clock);
        tarjeta.Cargar(5000);

        // Primer día - 3 viajes (0 + 0 + 3000)
        colectivoInterurbano.PagarCon(tarjeta);
        clock.Now = clock.Now.AddMinutes(10);
        colectivoInterurbano.PagarCon(tarjeta);
        clock.Now = clock.Now.AddMinutes(10);
        colectivoInterurbano.PagarCon(tarjeta);
        double saldoFinDia1 = tarjeta.Saldo; // 5000 - 3000 = 2000

        // Act - Avanzar al día siguiente
        clock.Now = clock.Now.AddDays(1);
        colectivoInterurbano.PagarCon(tarjeta); // Primer viaje del nuevo día (gratis)

        // Assert
        Assert.AreEqual(2000, tarjeta.Saldo); // No descuenta, vuelve a ser gratis
    }

    [Test]
    public void MedioBoletoInterurbano_SeReiniciaAlDiaSiguiente()
    {
        var tarjeta = new MedioBoletoInterurbano(clock);
        tarjeta.Cargar(10000);

        // Primer día - 3 viajes (1500 + 1500 + 3000)
        colectivoInterurbano.PagarCon(tarjeta);
        clock.Now = clock.Now.AddMinutes(10);
        colectivoInterurbano.PagarCon(tarjeta);
        clock.Now = clock.Now.AddMinutes(10);
        colectivoInterurbano.PagarCon(tarjeta);
        double saldoFinDia1 = tarjeta.Saldo; // 10000 - 6000 = 4000

        // Act - Avanzar al día siguiente
        clock.Now = clock.Now.AddDays(1);
        colectivoInterurbano.PagarCon(tarjeta); // Primer viaje del nuevo día (1500)

        // Assert
        Assert.AreEqual(4000 - 1500, tarjeta.Saldo);
    }

    [Test]
    public void TarjetasInterurbanas_UsanTarifaCorrectaEnColectivoUrbano()
    {
        // Arrange
        var tarjetaNormal = new TarjetaInterurbana();
        var medioBoleto = new MedioBoletoInterurbano(clock);
        var gratuito = new BoletoGratuitoInterurbano(clock);

        tarjetaNormal.Cargar(5000);
        medioBoleto.Cargar(5000);
        gratuito.Cargar(5000);

        // Act - Pagar en colectivo URBANO
        bool pagoNormal = colectivo.PagarCon(tarjetaNormal);
        bool pagoMedio = colectivo.PagarCon(medioBoleto);
        bool pagoGratuito = colectivo.PagarCon(gratuito);

        // Assert - Deberían usar sus tarifas interurbanas
        Assert.IsTrue(pagoNormal);
        Assert.AreEqual(5000 - 3000, tarjetaNormal.Saldo);

        Assert.IsTrue(pagoMedio);
        Assert.AreEqual(5000 - 1500, medioBoleto.Saldo);

        Assert.IsTrue(pagoGratuito);
        Assert.AreEqual(5000, gratuito.Saldo);
    }

    [Test]
    public void MedioBoletoInterurbano_RespetaRestriccion5Minutos()
    {
        var tarjeta = new MedioBoletoInterurbano(clock);
        tarjeta.Cargar(5000);

        // Primer viaje exitoso
        colectivoInterurbano.PagarCon(tarjeta);

        // Act - Segundo viaje en menos de 5 minutos
        clock.Now = clock.Now.AddMinutes(3);
        bool segundoPago = colectivoInterurbano.PagarCon(tarjeta);

        // Assert
        Assert.IsFalse(segundoPago);
        Assert.AreEqual(5000 - 1500, tarjeta.Saldo);
    }

    [Test]
    public void BoletoGratuitoInterurbano_TercerViajePagaCompleto()
    {
        var tarjeta = new BoletoGratuitoInterurbano(clock);
        tarjeta.Cargar(5000);

        // Primeros dos viajes gratis
        colectivoInterurbano.PagarCon(tarjeta);
        clock.Now = clock.Now.AddMinutes(10);
        colectivoInterurbano.PagarCon(tarjeta);

        // Act - Tercer viaje (paga completo)
        clock.Now = clock.Now.AddMinutes(10);
        bool pagoExitoso = colectivoInterurbano.PagarCon(tarjeta);

        // Assert
        Assert.IsTrue(pagoExitoso);
        Assert.AreEqual(5000 - 3000, tarjeta.Saldo); // Verificar que se descontaron 3000
    }

    [Test]
    public void BoletoGratuitoInterurbano_PrimerosDosViajesGratis()
    {
        var tarjeta = new BoletoGratuitoInterurbano(clock);
        tarjeta.Cargar(5000);

        // Act - Primer viaje (gratis)
        colectivoInterurbano.PagarCon(tarjeta);
        double saldoDespuesPrimero = tarjeta.Saldo;

        // Segundo viaje (gratis)
        clock.Now = clock.Now.AddMinutes(10);
        colectivoInterurbano.PagarCon(tarjeta);
        double saldoDespuesSegundo = tarjeta.Saldo;

        // Assert
        Assert.AreEqual(5000, saldoDespuesPrimero);
        Assert.AreEqual(5000, saldoDespuesSegundo);
    }

    [Test]
    public void MedioBoletoInterurbano_TercerViajePagaCompleto()
    {
        var tarjeta = new MedioBoletoInterurbano(clock);
        tarjeta.Cargar(10000);

        // Act - Primer viaje (1500)
        colectivoInterurbano.PagarCon(tarjeta);
        double saldoDespuesPrimero = tarjeta.Saldo;

        // Segundo viaje (1500)
        clock.Now = clock.Now.AddMinutes(10);
        colectivoInterurbano.PagarCon(tarjeta);
        double saldoDespuesSegundo = tarjeta.Saldo;

        // Tercer viaje (3000 - completo)
        clock.Now = clock.Now.AddMinutes(10);
        colectivoInterurbano.PagarCon(tarjeta);
        double saldoDespuesTercero = tarjeta.Saldo;

        // Assert
        Assert.AreEqual(10000 - 1500, saldoDespuesPrimero);
        Assert.AreEqual(8500 - 1500, saldoDespuesSegundo);
        Assert.AreEqual(7000 - 3000, saldoDespuesTercero);
    }

    [Test]
    public void MedioBoletoInterurbano_PagaMitadDeTarifa()
    {
        // Arrange
        var tarjeta = new MedioBoletoInterurbano(clock);
        tarjeta.Cargar(5000);

        // Act
        bool pagoExitoso = colectivoInterurbano.PagarCon(tarjeta);

        // Assert
        Assert.IsTrue(pagoExitoso);
        Assert.AreEqual(5000 - 1500, tarjeta.Saldo);
        Assert.AreEqual("Medio Boleto (Interurbana)", colectivoInterurbano.UltimoBoleto.TipoTarjeta);
        Assert.AreEqual(1500, colectivoInterurbano.UltimoBoleto.MontoDescontado);
    }

    [Test]
    public void TarjetaInterurbana_PagaTarifaCompleta()
    {
        // Arrange
        var tarjeta = new TarjetaInterurbana();
        tarjeta.Cargar(5000);

        // Act
        bool pagoExitoso = colectivoInterurbano.PagarCon(tarjeta);

        // Assert
        Assert.IsTrue(pagoExitoso);
        Assert.AreEqual(5000 - 3000, tarjeta.Saldo);
        Assert.AreEqual("Tarjeta Normal (Interurbana)", colectivoInterurbano.UltimoBoleto.TipoTarjeta);
        Assert.AreEqual(3000, colectivoInterurbano.UltimoBoleto.MontoDescontado);
    }

    // ==================== TESTS DE USO FRECUENTE ====================
    [Test]
    public void ObtenerMontoAPagar_AplicaDescuentosCorrectamenteSegunRangoDeViajes()
    {
        var clock = new FakeClock(new DateTime(2025, 11, 15, 10, 0, 0));
        var tarjeta = new UsoFrecuente(clock);
        var colectivoLocal = new Colectivo("144", clock);

        tarjeta.Cargar(30000); tarjeta.Cargar(30000); tarjeta.Cargar(30000);
        tarjeta.Cargar(30000); tarjeta.Cargar(30000); tarjeta.Cargar(30000);
        tarjeta.Cargar(30000); tarjeta.Cargar(30000); tarjeta.Cargar(30000);
        tarjeta.Cargar(30000); // Saldo suficiente

        for (int i = 1; i <= 29; i++)
        {
            Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
            clock.AdvanceMinutes(10);
        }

        for (int i = 30; i <= 59; i++)
        {
            Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
            Assert.That(colectivoLocal.UltimoBoleto?.MontoDescontado, Is.EqualTo(1264).Within(0.01));
            clock.AdvanceMinutes(10);
        }

        for (int i = 60; i <= 80; i++)
        {
            Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
            Assert.That(colectivoLocal.UltimoBoleto?.MontoDescontado, Is.EqualTo(1185).Within(0.01));
            clock.AdvanceMinutes(10);
        }

        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        Assert.That(colectivoLocal.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580).Within(0.01));
    }

    [Test]
    public void ContadorDeViajes_SeReiniciaCorrectamenteAlCambiarDeMes()
    {
        var clock = new FakeClock(new DateTime(2025, 11, 30, 23, 50, 0));
        var tarjeta = new UsoFrecuente(clock);
        var colectivoLocal = new Colectivo("144", clock);

        tarjeta.Cargar(30000); tarjeta.Cargar(30000); tarjeta.Cargar(30000); tarjeta.Cargar(10000);

        // Hacer algunos viajes en noviembre
        for (int i = 0; i < 10; i++)
        {
            colectivoLocal.PagarCon(tarjeta);
            clock.AdvanceMinutes(10);
        }

        // Cambiar a diciembre
        clock.Now = new DateTime(2025, 12, 1, 0, 0, 0);

        // Primer viaje del nuevo mes → debería ser tarifa completa (1580)
        colectivoLocal.PagarCon(tarjeta);
        Assert.That(colectivoLocal.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580).Within(0.01),
            "El primer viaje del nuevo mes debería cobrar tarifa completa (1580)");
    }

    [Test]
    public void Pagar_ConSaldoInsuficiente_NoIncrementaContadorDeViajes()
    {
        var clock = new FakeClock(new DateTime(2025, 11, 20, 8, 0, 0));
        var tarjeta = new UsoFrecuente(clock);
        var colectivoLocal = new Colectivo("144", clock);

        tarjeta.Cargar(2000);

        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        clock.AdvanceMinutes(10);
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        clock.AdvanceMinutes(10);

        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.False);
        Assert.That(tarjeta.Saldo, Is.EqualTo(-1160).Within(0.01));

        tarjeta.Cargar(10000);
        clock.AdvanceMinutes(10);
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        Assert.That(colectivoLocal.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580).Within(0.01));
    }

    // ==================== TESTS DE TRASBORDO ====================

    //------------------------------------------TEST TRANSBORDO ITERACION 5 ------------------------------//
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
    //------------------------------------------TEST TRANSBORDO ITERACION 5 ------------------------------//
    [Test]
    public void Trasbordo_TienePrioridadSobreMedioBoleto_CuandoCorresponde()
    {
        var clock = new FakeClock(new DateTime(2025, 4, 5, 10, 0, 0)); // Día hábil
        var medio = new MedioBoleto(clock);
        medio.Cargar(10000);

        var colectivo144Roja = new Colectivo("144 Roja", clock);
        var colectivo144Negra = new Colectivo("144 Negra", clock);

        // Primer viaje: paga medio boleto
        colectivo144Roja.PagarCon(medio);
        Assert.That(colectivo144Roja.UltimoBoleto?.MontoDescontado, Is.EqualTo(790));

        clock.AdvanceMinutes(30);

        // Segundo viaje: cumple trasbordo → debe salir GRATIS, NO medio boleto ($790)
        colectivo144Negra.PagarCon(medio);
        Assert.That(colectivo144Negra.UltimoBoleto?.MontoDescontado, Is.EqualTo(0));
        Assert.That(colectivo144Negra.UltimoBoleto?.EsTrasbordo, Is.True);
        Assert.That(colectivo144Negra.UltimoBoleto?.TipoTarjeta, Is.EqualTo("Medio Boleto")); // opcional
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

    [Test]
    public void Trasbordo_NoGratuito_FueraDeHorario()
    {
        var clock = new FakeClock(new DateTime(2025, 4, 5, 23, 0, 0)); // Domingo 23:00
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(10000);

        var colectivoA = new Colectivo("144 Roja", clock);
        var colectivoB = new Colectivo("144 Negra", clock);

        colectivoA.PagarCon(tarjeta);
        clock.AdvanceMinutes(20);
        colectivoB.PagarCon(tarjeta);

        Assert.That(colectivoB.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580));
        Assert.That(colectivoB.UltimoBoleto?.EsTrasbordo, Is.False);
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
}
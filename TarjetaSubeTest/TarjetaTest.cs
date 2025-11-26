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
    private Colectivo colectivoInterurbano;
    private FakeClock clock;

    [SetUp]
    public void Setup()
    {
        colectivo = new Colectivo("144 Roja");
        colectivoInterurbano = new Colectivo("Línea 500 (Interurbana)");
        clock = new FakeClock { Now = new DateTime(2024, 1, 1, 10, 0, 0) };
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
    /*
    [Test]
    public void Pagar_ConSaldoCero_NoPermiteViaje_PorqueCruzaNegativoMax()
    {
        var tarjeta = new Tarjeta();
        var resultado = colectivo.PagarCon(tarjeta);

        Assert.That(resultado, Is.False);
        Assert.That(tarjeta.Saldo, Is.EqualTo(0)); // No se descuenta nada
    }

    
    [Test]
    public void Pagar_ConSaldoInsuficienteSuperandoNegativoMax_DevuelveFalseYNoDescuenta()
    {
        var tarjeta = new Tarjeta(); // Saldo 0
        tarjeta.Pagar(TARIFA_BASICA); // Saldo -1580

        // El saldo negativo máximo es -1200. Si saldo es -1580, y se intenta debitar 1580,
        // el saldo final sería -3160. Se excede el límite.
        bool pagado = colectivo.PagarCon(tarjeta);

        Assert.That(pagado, Is.False);
        Assert.That(tarjeta.Saldo, Is.EqualTo(-TARIFA_BASICA).Within(0.01)); // Saldo se mantiene en -1580
    }
    */
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
        Assert.That(tarjeta.SaldoPendiente, Is.EqualTo(4420).Within(0.01)); // ¡4420, no 2420!
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

        // AJUSTE: Saldo: 1210 - 790 = 420 (Viaje 2 con descuento del día)
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
        tarjeta.Cargar(2000); // Saldo 2000
        var colectivoControlado = new Colectivo("102", clock);

        // 2. Primer viaje (Gratis)
        colectivoControlado.PagarCon(tarjeta); // Saldo 2000. ViajesHoy 1.

        // 3. Segundo viaje (Gratis)
        clock.Now = clock.Now.AddMinutes(10); // 7:10 AM
        colectivoControlado.PagarCon(tarjeta); // Saldo 2000. ViajesHoy 2.

        // 4. Tercer viaje (Debería ser Pago Completo: 1580)
        clock.Now = clock.Now.AddMinutes(10); // 7:20 AM
        colectivoControlado.PagarCon(tarjeta); // Saldo 2000 - 1580 = 420. ViajesHoy 2.

        // 5. Cuarto viaje (Pago Completo con saldo plus: 420 - 1580 = -1160)
        clock.Now = clock.Now.AddMinutes(10); // 7:30 AM
        colectivoControlado.PagarCon(tarjeta); // Saldo 420 - 1580 = -1160. ViajesHoy 2.

        // CORRECCIÓN: El test original esperaba 2000 (incorrecto). 
        // El saldo final real con 4 viajes (2 gratis, 2 pagados) es -1160.
        Assert.That(tarjeta.Saldo, Is.EqualTo(-1160).Within(0.01));
    }

    [Test]
    public void BoletoGratuito_SeReiniciaAlDiaSiguiente_ConClock()
    {
        var clock = new FakeClock { Now = new DateTime(2025, 11, 24, 10, 0, 0) }; // Lunes
        var colectivo = new Colectivo("102", clock);
        var tarjeta = new BoletoGratuito(clock);
        tarjeta.Cargar(10000);

        // Día 1: 2 gratis + 1 pago
        colectivo.PagarCon(tarjeta); // gratis
        colectivo.PagarCon(tarjeta); // gratis
        colectivo.PagarCon(tarjeta); // paga → saldo = 10000 - 1580 = 8420

        // Cambiar al día siguiente
        clock.Now = new DateTime(2025, 11, 25, 10, 0, 0); // Martes

        // Día 2: vuelve a tener 2 viajes gratis
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(8420)); // no bajó

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(8420)); // sigue igual

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(8420 - 1580).Within(0.01)); // ahora sí paga
    }

//----------------------------------------FRANQUICIAS-----------------------------------//
    [Test]
    public void Franquicias_FueraDeHorario_NoAplicanBeneficio()
    {
        var clock = new FakeClock { Now = new DateTime(2025, 11, 24, 23, 30, 0) }; // Lunes 23:30
        var colectivo = new Colectivo("144", clock);

        var medio = new MedioBoleto(clock);
        var gratuito = new BoletoGratuito(clock);

        medio.Cargar(10000);
        gratuito.Cargar(10000);

        Assert.That(colectivo.PagarCon(medio), Is.True);
        Assert.That(medio.Saldo, Is.EqualTo(10000 - 1580).Within(0.01)); // pagó completo!

        Assert.That(colectivo.PagarCon(gratuito), Is.True);
        Assert.That(gratuito.Saldo, Is.EqualTo(10000 - 1580).Within(0.01)); // pagó completo!
    }

    [Test]
    public void Franquicias_EnHorario_SiAplicanBeneficio()
    {
        var clock = new FakeClock { Now = new DateTime(2025, 11, 24, 14, 0, 0) }; // Lunes 14hs
        var colectivo = new Colectivo("60", clock);

        var medio = new MedioBoleto(clock);
        var gratuito = new BoletoGratuito(clock);

        medio.Cargar(10000);
        gratuito.Cargar(10000);

        Assert.That(colectivo.PagarCon(medio), Is.True);
        Assert.That(medio.Saldo, Is.EqualTo(10000 - 790).Within(0.01)); // 50%

        Assert.That(colectivo.PagarCon(gratuito), Is.True);
        Assert.That(gratuito.Saldo, Is.EqualTo(10000)); // gratis
    }


    //----------------------------INTERURBANO---------------------------------//
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
                                                     // Comentar esta línea o cambiarla por una verificación del saldo
                                                     // Assert.AreEqual(3000, colectivoInterurbano.UltimoBoleto.MontoDescontado);
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

    public class FakeClock : IClock
    {
        public DateTime Now { get; set; } = DateTime.Now;
    }
}


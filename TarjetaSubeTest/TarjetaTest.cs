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
    private const double TARIFA_NORMAL = 1580.0;     // Viajes 1-29 y 81+
    private const double TARIFA_20_DTO = 1264.0;    // 1580 * 0.8 = 1264.0 (Viajes 30-59)
    private const double TARIFA_25_DTO = 1185.0;

    private FakeClock clock = null!;
    private Colectivo colectivo = null!;

    [SetUp]
    public void Setup()
    {
        clock = new FakeClock(new DateTime(2025, 1, 1, 0, 0, 0));
        colectivo = new Colectivo("144 Roja", clock);  
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

    /*
    [Test]
    public void Cargar_SaldoSuperaLimite_NoPermiteCarga()
    {
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(30000);
        tarjeta.Cargar(20000);
        tarjeta.Cargar(4000);
        tarjeta.Cargar(2000);

        Assert.That(tarjeta.Cargar(2000), Is.False);
        Assert.That(tarjeta.Saldo, Is.EqualTo(56000).Within(0.01));
    }
    */
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

    public class FakeClock : IClock
    {
        public DateTime FixedNow { get; set; }
        public DateTime Now => FixedNow;  // Esto es importante

        public FakeClock(DateTime initialTime) => FixedNow = initialTime;

        public void AdvanceMinutes(int minutes) => FixedNow = FixedNow.AddMinutes(minutes);
        public void AdvanceDays(int days) => FixedNow = FixedNow.AddDays(days);
        public void AdvanceMonths(int months) => FixedNow = FixedNow.AddMonths(months);
    }

    [Test]
    public void MedioBoleto_RestriccionDe5Minutoso()
    {
        var clock = new FakeClock(new DateTime(2025, 5, 1, 9, 0, 0));
        var tarjeta = new MedioBoleto(clock);
        var colectivo = new Colectivo("144");

        tarjeta.Cargar(2000);

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(1210).Within(0.01));

        clock.AdvanceMinutes(4);
        Assert.That(colectivo.PagarCon(tarjeta), Is.False);
        Assert.That(tarjeta.Saldo, Is.EqualTo(1210).Within(0.01));

        clock.FixedNow = new DateTime(2025, 5, 1, 9, 5, 0);
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(420).Within(0.01));
    }

    [Test]
    public void MedioBoleto_MaximoDosViajesConDescuentoPorDia_TercerViajeCobraCompleto()
    {
        var clock = new FakeClock(new DateTime(2025, 6, 10, 8, 0, 0));
        var tarjeta = new MedioBoleto(clock);
        var colectivo = new Colectivo("60");
        tarjeta.Cargar(5000);
        double saldoEsperado = 5000;

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        saldoEsperado -= 790;
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01));

        clock.AdvanceMinutes(6);
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        saldoEsperado -= 790;
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01));

        clock.AdvanceMinutes(6);
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        saldoEsperado -= 1580;
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01));

        clock.AdvanceMinutes(6);
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
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

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(-1160).Within(0.01));
    }

    [Test]
    public void BoletoGratuito_SeReiniciaAlDiaSiguiente_ConClock()
    {
        var clock = new FakeClock(new DateTime(2025, 4, 5, 10, 0, 0));
        var colectivo = new Colectivo("60", clock);
        var tarjeta = new BoletoGratuito(clock);
        tarjeta.Cargar(5000);

        colectivo.PagarCon(tarjeta);
        colectivo.PagarCon(tarjeta);
        colectivo.PagarCon(tarjeta);
        Assert.That(tarjeta.Saldo, Is.EqualTo(5000 - 1580).Within(0.01));

        clock.FixedNow = new DateTime(2025, 4, 6, 10, 0, 0);
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(5000 - 1580).Within(0.01));

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(tarjeta.Saldo, Is.EqualTo(5000 - 1580).Within(0.01));
    }

    [Test]
    public void Cargar_ExcedenteQuedaPendiente_SiSuperaElLimite()
    {
        var tarjeta = new Tarjeta();
        const double LIMITE_MAXIMO = 56000;

        const double MONTO_INICIAL = 30000;
        const double MONTO_CARGA_EXCESO = 30000;

        // 1. Cargamos un monto inicial
        Assert.That(tarjeta.Cargar(MONTO_INICIAL), Is.True, "La carga inicial de 30000 debe ser exitosa.");
        Assert.That(tarjeta.Saldo, Is.EqualTo(MONTO_INICIAL).Within(0.01)); // Saldo: 30000

        // 2. Intentamos cargar 30000. Saldo potencial: 30000 + 30000 = 60000 (Supera 56000)
        Assert.That(tarjeta.Cargar(MONTO_CARGA_EXCESO), Is.True, "La segunda carga debe ser exitosa (aunque parcial).");

        // 3. Verificamos que el saldo solo llegó al límite
        double saldoAcreditado = LIMITE_MAXIMO; // 56000
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoAcreditado).Within(0.01), "El saldo acreditado debe ser exactamente el límite máximo (56000).");

        // 4. Verificamos el excedente pendiente
        double excedentePendiente = (MONTO_INICIAL + MONTO_CARGA_EXCESO) - LIMITE_MAXIMO; // 60000 - 56000 = 4000
        Assert.That(tarjeta.SaldoPendiente, Is.EqualTo(excedentePendiente).Within(0.01), "El excedente de 4000 debe quedar como saldo pendiente.");
    }


    [Test]
    public void Pagar_AcreditaSaldoPendiente_DespuesDeCadaViaje()
    {
        var tarjeta = new Tarjeta();
        const double TARIFA = 1580;
        const double LIMITE_MAXIMO = 56000;
        const double MONTO_CARGA_1 = 30000;
        const double MONTO_CARGA_2 = 30000;
        const double EXCEDENTE_TOTAL = 4000; // (30000 + 30000) - 56000

        // 1. Dejamos la tarjeta con saldo pendiente.
        tarjeta.Cargar(MONTO_CARGA_1); // Saldo: 30000. Pendiente: 0.
        Assert.That(tarjeta.Cargar(MONTO_CARGA_2), Is.True, "La segunda carga debe ser parcial y exitosa.");

        // Saldo inicial acreditado: 56000. Pendiente: 4000.
        Assert.That(tarjeta.Saldo, Is.EqualTo(LIMITE_MAXIMO).Within(0.01));
        Assert.That(tarjeta.SaldoPendiente, Is.EqualTo(EXCEDENTE_TOTAL).Within(0.01));

        // --- PRIMER VIAJE: Descuenta tarifa y acredita inmediatamente ---

        // Saldo después del débito: 56000 - 1580 = 54420
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);

        // Saldo ACREDITADO: 1580 (lo que se usó)
        // Saldo Final: 54420 + 1580 = 56000 (Vuelve al límite)
        Assert.That(tarjeta.Saldo, Is.EqualTo(LIMITE_MAXIMO).Within(0.01), "El saldo debe volver al límite tras la acreditación.");

        // Nuevo Saldo Pendiente: 4000 - 1580 = 2420
        double saldoPendienteDespuesViaje1 = EXCEDENTE_TOTAL - TARIFA;
        Assert.That(tarjeta.SaldoPendiente, Is.EqualTo(saldoPendienteDespuesViaje1).Within(0.01), "El saldo pendiente debe reducirse en 1580.");


        // --- SEGUNDO VIAJE: Descuenta y acredita el resto del saldo pendiente ---

        // Saldo después del débito: 56000 - 1580 = 54420
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);

        // Saldo ACREDITADO: 1580 (lo que se usó)
        // Saldo Final: 54420 + 1580 = 56000 (Vuelve al límite)
        Assert.That(tarjeta.Saldo, Is.EqualTo(LIMITE_MAXIMO).Within(0.01), "El saldo debe volver al límite tras la segunda acreditación.");

        // Nuevo Saldo Pendiente: 2420 - 1580 = 840
        double saldoPendienteDespuesViaje2 = saldoPendienteDespuesViaje1 - TARIFA;
        Assert.That(tarjeta.SaldoPendiente, Is.EqualTo(saldoPendienteDespuesViaje2).Within(0.01), "El saldo pendiente final debe ser 840.");
    }

    [Test]
    public void Colectivo_EmiteBoletoCorrectoParaCadaTipoDeTarjeta_YDevuelveTrue()
    {
        var clock = new FakeClock(new DateTime(2025, 6, 6, 18, 30, 0));
        var colectivo = new Colectivo("144", clock);

        var normal = new Tarjeta(); normal.Cargar(10000);
        var medio = new MedioBoleto(clock); medio.Cargar(10000);
        var gratuito = new BoletoGratuito(clock); gratuito.Cargar(5000);
        var franquicia = new FranquiciaCompleta();

        Assert.That(colectivo.PagarCon(normal), Is.True);
        Assert.That(colectivo.UltimoBoleto?.TipoTarjeta, Is.EqualTo("Tarjeta Normal"));

        Assert.That(colectivo.PagarCon(gratuito), Is.True);
        Assert.That(colectivo.UltimoBoleto?.TipoTarjeta, Is.EqualTo("Boleto Gratuito"));

        Assert.That(colectivo.PagarCon(franquicia), Is.True);
        Assert.That(colectivo.UltimoBoleto?.TipoTarjeta, Is.EqualTo("Franquicia Completa"));

        clock.AdvanceMinutes(6);
        Assert.That(colectivo.PagarCon(medio), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(colectivo.UltimoBoleto?.TipoTarjeta, Is.EqualTo("Medio Boleto"));
            Assert.That(colectivo.UltimoBoleto?.MontoDescontado, Is.EqualTo(790));
        });
    }

    [Test]
    public void Boleto_RecuperaDeudaCorrectamente_CuandoSaldoNegativoPeroPermiteViaje()
    {
        var clock = new FakeClock(new DateTime(2025, 6, 6, 19, 0, 0));
        var colectivo = new Colectivo("60", clock);
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(2000);

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);

        var boleto = colectivo.UltimoBoleto;
        Assert.That(boleto?.SaldoRestante, Is.EqualTo(-1160).Within(0.01));
        Assert.That(boleto?.MontoDescontado, Is.EqualTo(1580).Within(0.01));
    }

    [Test]
    public void ObtenerMontoAPagar_AplicaDescuentosCorrectamenteSegunRangoDeViajes()
    {
        var clock = new FakeClock(new DateTime(2025, 11, 15, 10, 0, 0));
        var tarjeta = new UsoFrecuente(clock);
        tarjeta.Cargar(30000); tarjeta.Cargar(30000); tarjeta.Cargar(30000);
        tarjeta.Cargar(30000); tarjeta.Cargar(30000); tarjeta.Cargar(30000);
        tarjeta.Cargar(30000); tarjeta.Cargar(30000); tarjeta.Cargar(30000);
        tarjeta.Cargar(30000); // Saldo suficiente

        for (int i = 1; i <= 29; i++)
        {
            Assert.That(colectivo.PagarCon(tarjeta), Is.True);
            clock.AdvanceMinutes(10);
        }

        for (int i = 30; i <= 59; i++)
        {
            Assert.That(colectivo.PagarCon(tarjeta), Is.True);
            Assert.That(colectivo.UltimoBoleto?.MontoDescontado, Is.EqualTo(1264).Within(0.01));
            clock.AdvanceMinutes(10);
        }

        for (int i = 60; i <= 80; i++)
        {
            Assert.That(colectivo.PagarCon(tarjeta), Is.True);
            Assert.That(colectivo.UltimoBoleto?.MontoDescontado, Is.EqualTo(1185).Within(0.01));
            clock.AdvanceMinutes(10);
        }

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(colectivo.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580).Within(0.01));
    }

    [Test]
    public void ContadorDeViajes_SeReiniciaCorrectamenteAlCambiarDeMes()
    {
        var clock = new FakeClock(new DateTime(2025, 11, 30, 23, 50, 0));
        var tarjeta = new UsoFrecuente(clock);
        var colectivo = new Colectivo("144", clock);

        tarjeta.Cargar(30000); tarjeta.Cargar(30000); tarjeta.Cargar(30000); tarjeta.Cargar(10000);

        // Hacer algunos viajes en noviembre (no necesariamente 35)
        for (int i = 0; i < 10; i++)
        {
            colectivo.PagarCon(tarjeta);
            clock.AdvanceMinutes(10);
        }

        // Verificar que estamos en noviembre con algún descuento aplicado
        // (esto depende de cuántos viajes hayamos hecho)

        // Cambiar a diciembre
        clock.FixedNow = new DateTime(2025, 12, 1, 0, 0, 0);

        // Primer viaje del nuevo mes → debería ser tarifa completa (1580)
        // sin importar cuántos viajes se hicieron en el mes anterior
        colectivo.PagarCon(tarjeta);
        Assert.That(colectivo.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580).Within(0.01),
            "El primer viaje del nuevo mes debería cobrar tarifa completa (1580)");
    }


    [Test]
    public void Pagar_ConSaldoInsuficiente_NoIncrementaContadorDeViajes()
    {
        var clock = new FakeClock(new DateTime(2025, 11, 20, 8, 0, 0));
        var tarjeta = new UsoFrecuente(clock);
        tarjeta.Cargar(2000);

        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        clock.AdvanceMinutes(10);
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        clock.AdvanceMinutes(10);

        Assert.That(colectivo.PagarCon(tarjeta), Is.False);
        Assert.That(tarjeta.Saldo, Is.EqualTo(-1160).Within(0.01));

        tarjeta.Cargar(10000);
        clock.AdvanceMinutes(10);
        Assert.That(colectivo.PagarCon(tarjeta), Is.True);
        Assert.That(colectivo.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580).Within(0.01));
    }
}


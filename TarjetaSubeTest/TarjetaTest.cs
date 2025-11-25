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
    public void BoletoGratuito_NoMasDeDosGratisPorDia_ConClockControlado()
    {
        // 1. Configuración de reloj y tarjeta.
        var clock = new FakeClock { Now = new DateTime(2025, 11, 24, 7, 0, 0) }; // Lunes 7 AM
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

    // Se asume la existencia de FakeClock para los tests con control de tiempo.
    public class FakeClock : IClock
    {
        public DateTime Now { get; set; } = DateTime.Now;
    }
}
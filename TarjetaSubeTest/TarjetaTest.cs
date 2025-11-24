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

}

using NUnit.Framework;
using System;
using System.Linq;
using TarjetaSube;

[TestFixture]
public class MedioBoletoPagarConTests
{
    private static readonly double[] MontosValidos = { 2000, 3000, 4000, 5000, 8000, 10000, 15000, 20000, 25000, 30000 };
    private const double SALDO_NEGATIVO_MAX = -1200;
    private const double TARIFA_BASICA = 1580;
    private const double TARIFA_NORMAL = 1580.0;     // Viajes 1-29 y 81+
    private const double TARIFA_20_DTO = 1264.0;    // 1580 * 0.8 = 1264.0 (viajes 30-59)
    private const double TARIFA_25_DTO = 1185.0;

    private Colectivo colectivo;
    private Colectivo colectivoInterurbano;
    private FakeClock clock;

    [SetUp]
    public void Setup()
    {
        //inicializar el clock primero
        clock = new FakeClock { Now = new DateTime(2024, 1, 1, 10, 0, 0) };

        //asignar a los campos de la clase 
        colectivo = new Colectivo("144 Roja", clock);
        colectivoInterurbano = new Interurbano("Gálvez", clock);
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

    //======================= MEDIO BOLETO ==================================//

    //-------------------------------------------TEST DE PAGAR CON 2 DE ITERACION 5 -------------------------------------//

            /*Se pagan dos boletos a mitad de precio y el tercero a precio normal. Los mismo que el anterior.*/
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
    //------------------------ TEST DE PAGAR CON 1 ITERACION 5 -----------------------------//
    /*Se pueden pagar dos boletos a mitad de precio. Verificando que el tiempo correspondiente paso entre uno y otro,
                que hay saldo suficiente y que el saldo negativo también permite pagar medio boleto.*/
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
        colectivoLocal.PagarCon(tarjeta);
        saldoEsperado -= 790;
        

        clock.AdvanceMinutes(3); // Solo pasaron 3 minutos
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.False, "No debería poder pagar antes de los 5 minutos");
        Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01), "El saldo no debería cambiar cuando el pago es rechazado por tiempo");//0

        // Segundo viaje con medio boleto (790) - aún en el límite de 2 viajes por día
        clock.AdvanceMinutes(6);
        colectivoLocal.PagarCon(tarjeta);
        saldoEsperado -= 790;
        

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

    // ==================== TESTS DE TRASBORDO ====================//

    //------------------------------------------TEST DE TRANSBORDO ITERACION 5 ------------------------------//
    //Verificar que la misma pueda viajar con transbordo si se cumplen los requisitos.//
    [Test]
    public void Trasbordo_Gratuito_CuandoCumpleCondiciones()
    {
        var clock = new FakeClock(new DateTime(2025, 4, 5, 10, 0, 0));
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(10000);

        var colectivoA = new Colectivo("144 Roja", clock);
        var colectivoB = new Colectivo("144 Negra", clock); //lineas distintas  

        //paga primer colectivo
        Assert.That(colectivoA.PagarCon(tarjeta), Is.True);
        Assert.That(colectivoA.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580));
        Assert.That(colectivoA.UltimoBoleto?.EsTrasbordo, Is.False);

        clock.AdvanceMinutes(20); //pasa menos de 1 hs

        //paga segundo colectivo
        Assert.That(colectivoB.PagarCon(tarjeta), Is.True);
        Assert.That(colectivoB.UltimoBoleto?.MontoDescontado, Is.EqualTo(0)); 
        Assert.That(colectivoB.UltimoBoleto?.EsTrasbordo, Is.True);
    }
    //------------------------------------------TEST DE TRANSBORDO ITERACION 5 ------------------------------//

    //El transbordo tiene mas peso que el medio boleto por lo que si tiene viajes a mitad de previo disponibles
    //pero se cumplen los requisitos del transbordo debería viajar gratis en lugar de pagar.
    //Para este caso deberían testear los casos en los que se puede viajar y en los que no con transbordo.

    [Test]
    public void Trasbordo_TienePrioridadSobreMedioBoleto_CuandoCorresponde()
    {
        var clock = new FakeClock(new DateTime(2025, 4, 5, 10, 0, 0)); // Día hábil
        var medio = new MedioBoleto(clock);
        medio.Cargar(10000);

        var colectivo144Roja = new Colectivo("144 Roja", clock);
        var colectivo144Negra = new Colectivo("144 Negra", clock);

        // Primer viaje paga medio boleto
        colectivo144Roja.PagarCon(medio);
        Assert.That(colectivo144Roja.UltimoBoleto?.MontoDescontado, Is.EqualTo(790));

        clock.AdvanceMinutes(30); //pasan 30 mins / menos de una hora

        // Segundo viaje cumple trasbordo -> debe salir GRATIS, NO medio boleto ($790)
        colectivo144Negra.PagarCon(medio);
        Assert.That(colectivo144Negra.UltimoBoleto?.MontoDescontado, Is.EqualTo(0));
        Assert.That(colectivo144Negra.UltimoBoleto?.EsTrasbordo, Is.True); //prioriza el transbordo
    }

    [Test]
    public void Trasbordo_NoAplicaDespuesDeUnaHora_CuandoCorresponde()
    {
        var clock = new FakeClock(new DateTime(2025, 4, 5, 10, 0, 0)); // Día hábil
        var medio = new MedioBoleto(clock);
        medio.Cargar(10000);

        var colectivo144Roja = new Colectivo("144 Roja", clock);
        var colectivo144Negra = new Colectivo("144 Negra", clock);

        // Primer viaje paga medio boleto
        colectivo144Roja.PagarCon(medio);
        Assert.That(colectivo144Roja.UltimoBoleto?.MontoDescontado, Is.EqualTo(790));

        clock.AdvanceMinutes(61); // pasan 61 minutos / mas de una hora, es trasbordo entre 0 y 60 mins inclusive

        // Segundo viaje NO cumple trasbordo -> debe pagar medio boleto normal
        colectivo144Negra.PagarCon(medio);
        Assert.That(colectivo144Negra.UltimoBoleto?.MontoDescontado, Is.EqualTo(790)); // Paga medio boleto
        Assert.That(colectivo144Negra.UltimoBoleto?.EsTrasbordo, Is.False); // No aplica trasbordo
    }

    //tengo dos test mas de transbordo en la clase TarjetaTests.cs (Trasbordo_NoGratuito_SiMismaLinea y Trasbordo_NoGratuito_FueraDeHorario)

}
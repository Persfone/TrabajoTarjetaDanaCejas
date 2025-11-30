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
    private const double TARIFA_NORMAL = 1580.0; //viajes 1-29 y 81+
    private const double TARIFA_20_DTO = 1264.0; //1580 * 0.8 = 1264.0 (viajes 30-59)
    private const double TARIFA_25_DTO = 1185.0;
    private Colectivo colectivo;
    private Colectivo colectivoInterurbano;
    private FakeClock clock;

    [SetUp]
    public void Setup()
    {
        //inicializar el clock primero
        clock = new FakeClock { Now = new DateTime(2024, 1, 1, 10, 0, 0) };

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
        public void AdvanceTime(TimeSpan span) => Now = Now.Add(span);
    }

    // ==================== TEST BASICOS DE CARGA Y PAGO ====================
    [Test]
    public void Cargar_TodosLosMontosValidos_AceptaYActualizaSaldoCorrectamente()
    {
        var tarjeta = new Tarjeta();
        foreach (var monto in MontosValidos)
        {
            Assert.That(tarjeta.Cargar(monto), Is.True, $"debería aceptar cargar {monto}");
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
        Assert.That(colectivo.PagarCon(tarjeta), Is.True); //2000 -> 420
        Assert.That(colectivo.PagarCon(tarjeta), Is.True); //420 -> -1160
        Assert.That(tarjeta.Saldo, Is.EqualTo(-1160).Within(0.01));
        Assert.That(colectivo.PagarCon(tarjeta), Is.False); //-1160 -1580 = -2740 -> excede -1200
        Assert.That(tarjeta.Saldo, Is.EqualTo(-1160).Within(0.01)); //no cambio
    }

    [Test]
    public void Cargar_ConCreditoPendiente_AcreditaTodoPendienteYRestoASaldo()
    {
        var tarjeta = new Tarjeta();
        tarjeta.Cargar(30000);
        tarjeta.Cargar(30000); // 56000 + 4000 pendiente
        Assert.That(tarjeta.Saldo, Is.EqualTo(56000));
        Assert.That(tarjeta.SaldoPendiente, Is.EqualTo(4000));
        colectivo.PagarCon(tarjeta); // 56000 -> 54420 -> acredita 1580 del pendiente
        //saldo = 56000, pendiente = 2420
        tarjeta.Cargar(2000); //todo va a pendiente (no hay espacio)
        Assert.That(tarjeta.Saldo, Is.EqualTo(56000));
        Assert.That(tarjeta.SaldoPendiente, Is.EqualTo(4420).Within(0.01)); // 4420
    }

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

    var medio = new MedioBoleto(clock);

    var gratis = new BoletoGratuito(clock);
    var franquicia = new FranquiciaCompleta();
    normal.Cargar(10000);
    medio.Cargar(10000);
    //tarjeta normal (paga 1580)
    Assert.That(colectivo.PagarCon(normal), Is.True);
    Assert.That(normal.Saldo, Is.EqualTo(10000 - 1580).Within(0.01)); //saldo: 8420
                                                                      //medio boleto (paga 790)
    Assert.That(colectivo.PagarCon(medio), Is.True);
    Assert.That(medio.Saldo, Is.EqualTo(10000 - 790).Within(0.01)); //saldo: 9210 
                                                                    //boleto gratuito (paga 0)
    Assert.That(colectivo.PagarCon(gratis), Is.True);
    //franquicia completa (paga 0)
    Assert.That(colectivo.PagarCon(franquicia), Is.True);
}

// ====================TEST DE FRANQUICIAS ====================
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

/*
//-------------------------------------------test pagar con 2 de iteracion 5 -----------------------------//
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

//------------------------ test pagar con 1 iteracion 5 -----------------------------//
[Test]
public void MedioBoleto_MaximoDosViajesConDescuentoPorDia_ConSaldoNegativo()
{
    var clock = new FakeClock(new DateTime(2025, 6, 10, 8, 0, 0));
    var tarjeta = new MedioBoleto(clock);
    var colectivoLocal = new Colectivo("60", clock);
    // carga 2000
    tarjeta.Cargar(2000);
    double saldoEsperado = 2000;
    // paga 2 viajes cada 6 minutos para q los cobre y sea medio boleto
    // primer viaje con medio boleto (790)
    Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True, "primer viaje debería ser exitoso con medio boleto");
    saldoEsperado -= 790;
    Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01), $"después del primer viaje, saldo debería ser {saldoEsperado}");//-1210
    clock.AdvanceMinutes(3); // solo pasaron 3 minutos
    Assert.That(colectivoLocal.PagarCon(tarjeta), Is.False, "no debería poder pagar antes de los 5 minutos");
    Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01), "el saldo no debería cambiar cuando el pago es rechazado por tiempo");//0
    // segundo viaje con medio boleto (790) - aún en el límite de 2 viajes por día
    clock.AdvanceMinutes(6);
    Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True, "segundo viaje debería ser exitoso con medio boleto");
    saldoEsperado -= 790;
    Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01), $"después del segundo viaje, saldo debería ser {saldoEsperado}"); //420
    // pasa 1 día (se reinicia el contador de medio boleto)
    clock.AdvanceDays(1);
    // vuelve a pagar 1 viaje - debería aplicar medio boleto nuevamente
    Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True, "cuarto viaje (nuevo día) debería ser exitoso con medio boleto");
    saldoEsperado -= 790;
    Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01), $"después del cuarto viaje (nuevo día), saldo debería ser {saldoEsperado} (medio boleto reiniciado)"); // -370
    clock.AdvanceMinutes(6);
    Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True, "quinto viaje (nuevo día) debería ser exitoso con medio boleto");
    saldoEsperado -= 790;
    Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01), $"después del cuarto viaje (nuevo día), saldo debería ser {saldoEsperado}"); //-1160
    clock.AdvanceMinutes(6);
    Assert.That(colectivoLocal.PagarCon(tarjeta), Is.False, "cuarto viaje debería fallar por exceso de saldo negativo");
    Assert.That(tarjeta.Saldo, Is.EqualTo(saldoEsperado).Within(0.01), "el saldo no debería cambiar cuando el pago es rechazado por saldo insuficiente"); //-1950

}
*/

[Test]
public void BoletoGratuito_NoMasDeDosGratisPorDia_ConClockControlado()
{
    var clock = new FakeClock(new DateTime(2025, 4, 5, 7, 0, 0));
    var tarjeta = new BoletoGratuito(clock);
    tarjeta.Cargar(2000);
    var colectivoControlado = new Colectivo("102", clock);
    colectivoControlado.PagarCon(tarjeta); //saldo 2000. viajeshoy 1.
    clock.AdvanceMinutes(10);
    colectivoControlado.PagarCon(tarjeta); //saldo 2000. viajeshoy 2.
    clock.AdvanceMinutes(10);
    colectivoControlado.PagarCon(tarjeta); //saldo 2000 - 1580 = 420. viajeshoy 2.
    clock.AdvanceMinutes(10);
    colectivoControlado.PagarCon(tarjeta); //saldo 420 - 1580 = -1160. viajeshoy 2.
    Assert.That(tarjeta.Saldo, Is.EqualTo(-1160).Within(0.01));
}

[Test]
public void BoletoGratuito_SeReiniciaAlDiaSiguiente_ConClock()
{
    var clock = new FakeClock(new DateTime(2025, 11, 24, 10, 0, 0)); //lunes
    var colectivoLocal = new Colectivo("102", clock);
    var tarjeta = new BoletoGratuito(clock);
    tarjeta.Cargar(10000);
    //día 1: 2 gratis + 1 pago
    colectivoLocal.PagarCon(tarjeta); //gratis
    colectivoLocal.PagarCon(tarjeta); //gratis
    colectivoLocal.PagarCon(tarjeta); //paga -> saldo = 10000 - 1580 = 8420
                                      //cambiar al día siguiente
    clock.Now = new DateTime(2025, 11, 25, 10, 0, 0); //martes
                                                      //día 2: vuelve a tener 2 viajes gratis
    Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
    Assert.That(tarjeta.Saldo, Is.EqualTo(8420)); //no bajo
    Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
    Assert.That(tarjeta.Saldo, Is.EqualTo(8420)); //sigue igual
    Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
    Assert.That(tarjeta.Saldo, Is.EqualTo(8420 - 1580).Within(0.01)); // ahora si paga
}

// ==================== TESTS DE HORARIO DE FRANQUICIAS ====================
[Test]
public void Franquicias_FueraDeHorario_NoAplicanBeneficio()
{
    var clock = new FakeClock(new DateTime(2025, 11, 24, 23, 30, 0)); //lunes 23:30
    var colectivoLocal = new Colectivo("144", clock);
    var medio = new MedioBoleto(clock);
    var gratuito = new BoletoGratuito(clock);
    medio.Cargar(10000);
    gratuito.Cargar(10000);
    Assert.That(colectivoLocal.PagarCon(medio), Is.True);
    Assert.That(medio.Saldo, Is.EqualTo(10000 - 1580).Within(0.01)); // pago completo
    Assert.That(colectivoLocal.PagarCon(gratuito), Is.True);
    Assert.That(gratuito.Saldo, Is.EqualTo(10000 - 1580).Within(0.01)); //pago completo
}

[Test]
public void Franquicias_EnHorario_SiAplicanBeneficio()
{
    var clock = new FakeClock(new DateTime(2025, 11, 24, 14, 0, 0)); //lunes 14hs
    var colectivoLocal = new Colectivo("60", clock);
    var medio = new MedioBoleto(clock);
    var gratuito = new BoletoGratuito(clock);
    medio.Cargar(10000);
    gratuito.Cargar(10000);
    Assert.That(colectivoLocal.PagarCon(medio), Is.True);
    Assert.That(medio.Saldo, Is.EqualTo(10000 - 790).Within(0.01)); //50%
    Assert.That(colectivoLocal.PagarCon(gratuito), Is.True);
    Assert.That(gratuito.Saldo, Is.EqualTo(10000)); //gratis
}

// ==================== TEST DE FRANQUICIAS INTERURBANAS ====================
[Test]
public void BoletoGratuito_Interurbano_SeReiniciaAlDiaSiguiente()
{
    // usamos la clase tarjeta base, y el colectivo interurbano (new interurbano)
    var tarjeta = new BoletoGratuito(clock);
    tarjeta.Cargar(5000);
    //primer día - 3 viajes (0 + 0 + 3000)
    //colectivo interurbano (3000)
    colectivoInterurbano.PagarCon(tarjeta); //viaje 1: gratis (saldo: 5000)
    clock.Now = clock.Now.AddMinutes(10);
    colectivoInterurbano.PagarCon(tarjeta); //viaje 2: gratis (saldo: 5000)
    clock.Now = clock.Now.AddMinutes(10);
    colectivoInterurbano.PagarCon(tarjeta); //viaje 3: paga completo (3000)
    double saldoFinDia1 = tarjeta.Saldo; //5000 - 3000 = 2000
                                         //avanzar al día siguiente
    clock.Now = clock.Now.AddDays(1);
    colectivoInterurbano.PagarCon(tarjeta); //primer viaje del nuevo día (gratis)
                                           
    Assert.AreEqual(2000, tarjeta.Saldo); //no descuenta, se reinicia y vuelve a ser gratis
}

[Test]
public void MedioBoleto_Interurbano_SeReiniciaAlDiaSiguiente()
{
    // usamos la clase tarjeta base, y el colectivo interurbano
    var tarjeta = new MedioBoleto(clock);
    tarjeta.Cargar(10000);
    // primer día - 3 viajes (1500 + 1500 + 3000)
    // colectivo interurbano (3000)
    colectivoInterurbano.PagarCon(tarjeta); // viaje 1: medio (1500)
    clock.Now = clock.Now.AddMinutes(10);
    colectivoInterurbano.PagarCon(tarjeta); // viaje 2: medio (1500)
    clock.Now = clock.Now.AddMinutes(10);
    colectivoInterurbano.PagarCon(tarjeta); // viaje 3: completo (3000)
    double saldoFinDia1 = tarjeta.Saldo; // 10000 - 1500 - 1500 - 3000 = 4000
                                         //avanzar al día siguiente
    clock.Now = clock.Now.AddDays(1);
    colectivoInterurbano.PagarCon(tarjeta); // primer viaje del nuevo día (1500)
                                          
    Assert.AreEqual(4000 - 1500, tarjeta.Saldo); // vuelve a pagar medio boleto
}

[Test]
public void TarjetasDeFranquicia_UsanTarifaUrbana_EnColectivoUrbano()
{
    //usamos las clases unificadas y el colectivo urbano
    var tarjetaNormal = new Tarjeta();
    var medioBoleto = new MedioBoleto(clock);
    var gratuito = new BoletoGratuito(clock);
    tarjetaNormal.Cargar(5000);
    medioBoleto.Cargar(5000);
    gratuito.Cargar(5000);
    //pagar en colectivo urbano (tarifa base = 1580)
    bool pagoNormal = colectivo.PagarCon(tarjetaNormal);
    bool pagoMedio = colectivo.PagarCon(medioBoleto);
    bool pagoGratuito = colectivo.PagarCon(gratuito);
    //deben usar la tarifa urbana (1580 / 790)
    Assert.IsTrue(pagoNormal);
    Assert.AreEqual(5000 - 1580, tarjetaNormal.Saldo); // tarifa normal (1580)
    Asert.IsTrue(pagoMedio);
    // 1580 / 2 = 790
    Assert.AreEqual(5000 - 790, medioBoleto.Saldo); // medio boleto urbano (790)
    Assert.IsTrue(pagoGratuito);
    Assert.AreEqual(5000, gratuito.Saldo); // gratis
}

[Test]
public void MedioBoleto_Interurbano_RespetaRestriccion5Minutos()
{
    //usamos la clase tarjeta base, y el colectivo interurbano (3000)
    var tarjeta = new MedioBoleto(clock);
    tarjeta.Cargar(5000);
    const double TARIFA_MEDIO_INTERURBANO = 1500;
    //primer viaje exitoso (1500)
    colectivoInterurbano.PagarCon(tarjeta);
    //segundo viaje en menos de 5 minutos
    clock.Now = clock.Now.AddMinutes(3);
    bool segundoPago = colectivoInterurbano.PagarCon(tarjeta);

    Assert.IsFalse(segundoPago);
    Assert.AreEqual(5000 - TARIFA_MEDIO_INTERURBANO, tarjeta.Saldo);
}

[Test]
public void BoletoGratuito_Interurbano_TercerViajePagaCompleto()
{
    //usamos la clase tarjeta base, y el colectivo interurbano (3000)
    var tarjeta = new BoletoGratuito(clock);
    tarjeta.Cargar(5000);
    const double TARIFA_COMPLETA_INTERURBANA = 3000;
    //primeros dos viajes gratis
    colectivoInterurbano.PagarCon(tarjeta);
    clock.Now = clock.Now.AddMinutes(10);
    colectivoInterurbano.PagarCon(tarjeta);
    //tercer viaje (paga completo 3000)
    clock.Now = clock.Now.AddMinutes(10);
    bool pagoExitoso = colectivoInterurbano.PagarCon(tarjeta);

    Assert.IsTrue(pagoExitoso);
    Assert.AreEqual(5000 - TARIFA_COMPLETA_INTERURBANA, tarjeta.Saldo);
}

[Test]
public void BoletoGratuito_Interurbano_PrimerosDosViajesGratis()
{
    //usamos la clase tarjeta base, y el colectivo interurbano (3000)
    var tarjeta = new BoletoGratuito(clock);
    tarjeta.Cargar(5000);
    //primer viaje (gratis)
    colectivoInterurbano.PagarCon(tarjeta);
    double saldoDespuesPrimero = tarjeta.Saldo;
    //segundo viaje (gratis)
    clock.Now = clock.Now.AddMinutes(10);
    colectivoInterurbano.PagarCon(tarjeta);
    double saldoDespuesSegundo = tarjeta.Saldo;

    Assert.AreEqual(5000, saldoDespuesPrimero);
    Assert.AreEqual(5000, saldoDespuesSegundo);
}

[Test]
public void MedioBoleto_Interurbano_TercerViajePagaCompleto()
{
    //usamos la clase tarjeta base, y el colectivo interurbano (3000)
    var tarjeta = new MedioBoleto(clock);
    tarjeta.Cargar(10000);
    const double TARIFA_MEDIO_INTERURBANO = 1500;
    const double TARIFA_COMPLETA_INTERURBANA = 3000;
    //primer viaje (1500)
    colectivoInterurbano.PagarCon(tarjeta);
    double saldoDespuesPrimero = tarjeta.Saldo;
    //segundo viaje (1500)
    clock.Now = clock.Now.AddMinutes(10);
    colectivoInterurbano.PagarCon(tarjeta);
    double saldoDespuesSegundo = tarjeta.Saldo;
    //tercer viaje (3000 - completo)
    clock.Now = clock.Now.AddMinutes(10);
    colectivoInterurbano.PagarCon(tarjeta);
    double saldoDespuesTercero = tarjeta.Saldo;

    Assert.AreEqual(10000 - TARIFA_MEDIO_INTERURBANO, saldoDespuesPrimero);
    Assert.AreEqual(8500 - TARIFA_MEDIO_INTERURBANO, saldoDespuesSegundo);
    Assert.AreEqual(7000 - TARIFA_COMPLETA_INTERURBANA, saldoDespuesTercero);
}

[Test]
public void MedioBoleto_Interurbano_PagaMitadDeTarifa()
{
    //usamos la clase medioboleto unificada
    var tarjeta = new MedioBoleto(clock);
    tarjeta.Cargar(5000);
    const double TARIFA_MEDIO_INTERURBANO = 1500; //3000 / 2
                                                  
    bool pagoExitoso = colectivoInterurbano.PagarCon(tarjeta);

    Assert.IsTrue(pagoExitoso);
    Assert.AreEqual(5000 - TARIFA_MEDIO_INTERURBANO, tarjeta.Saldo);
    //el tipo de tarjeta ya no debe decir "interurbana", solo el tipo de franquicia
    Assert.AreEqual("Medio Boleto", colectivoInterurbano.UltimoBoleto.TipoTarjeta);
    Assert.AreEqual(TARIFA_MEDIO_INTERURBANO, colectivoInterurbano.UltimoBoleto.MontoDescontado);
}

[Test]
public void TarjetaNormal_Interurbano_PagaTarifaCompleta()
{

    var tarjeta = new Tarjeta();
    tarjeta.Cargar(5000);
    const double TARIFA_COMPLETA_INTERURBANA = 3000;

    bool pagoExitoso = colectivoInterurbano.PagarCon(tarjeta);

    Assert.IsTrue(pagoExitoso);
    Assert.AreEqual(5000 - TARIFA_COMPLETA_INTERURBANA, tarjeta.Saldo);
    / el tipo de tarjeta ya no debe decir "interurbana", solo el tipo base
    Assert.AreEqual("Tarjeta Normal", colectivoInterurbano.UltimoBoleto.TipoTarjeta);
    Assert.AreEqual(TARIFA_COMPLETA_INTERURBANA, colectivoInterurbano.UltimoBoleto.MontoDescontado);
}

// ==================== TESTS DE USO FRECUENTE ====================
[Test]
public void ObtenerMontoAPagar_AplicaDescuentosCorrectamenteSegunRangoDeViajes()
{

    var clock = new FakeClock(new DateTime(2025, 11, 15, 10, 0, 0));
    var tarjeta = new Tarjeta(clock);
    var colectivoLocal = new Colectivo("144", clock);

    tarjeta.Cargar(30000); tarjeta.Cargar(30000); tarjeta.Cargar(30000);
    tarjeta.Cargar(30000); tarjeta.Cargar(30000); tarjeta.Cargar(30000);
    tarjeta.Cargar(30000); tarjeta.Cargar(30000); tarjeta.Cargar(30000);
    tarjeta.Cargar(30000);
    // viajes 1-29: tarifa normal (1580)
    for (int i = 1; i <= 29; i++)
    {
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        // verificar que el monto descontado sea 1580
        Assert.That(colectivoLocal.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580).Within(0.01));
        clock.AdvanceMinutes(10);
    }
    // viajes 30-59: 20% de descuento (1580 * 0.8 = 1264)
    for (int i = 30; i <= 59; i++)
    {
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        Assert.That(colectivoLocal.UltimoBoleto?.MontoDescontado, Is.EqualTo(1264).Within(0.01));
        clock.AdvanceMinutes(10);
    }
    // viajes 60-80: 25% de descuento (1580 * 0.75 = 1185)
    for (int i = 60; i <= 80; i++)
    {
        Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
        Assert.That(colectivoLocal.UltimoBoleto?.MontoDescontado, Is.EqualTo(1185).Within(0.01));
        clock.AdvanceMinutes(10);
    }
    // viaje 81: vuelve a tarifa normal (1580)
    Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
    Assert.That(colectivoLocal.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580).Within(0.01));
}

[Test]
public void ContadorDeViajes_SeReiniciaCorrectamenteAlCambiarDeMes()
{
    var clock = new FakeClock(new DateTime(2025, 11, 30, 23, 50, 0));
    // cambio: se usa tarjeta en lugar de usofrecuente
    var tarjeta = new Tarjeta(clock);
    var colectivoLocal = new Colectivo("144", clock);
    tarjeta.Cargar(30000); tarjeta.Cargar(30000); tarjeta.Cargar(30000); tarjeta.Cargar(10000);
    // hacer algunos viajes en noviembre
    for (int i = 0; i < 10; i++)
    {
        colectivoLocal.PagarCon(tarjeta);
        clock.AdvanceMinutes(10);
    }
    // cambiar a diciembre
    clock.Now = new DateTime(2025, 12, 1, 0, 0, 0);
    // primer viaje del nuevo mes → debería ser tarifa completa (1580)
    colectivoLocal.PagarCon(tarjeta);
    Assert.That(colectivoLocal.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580).Within(0.01),
        "el primer viaje del nuevo mes debería cobrar tarifa completa (1580)");
}

[Test]
public void Pagar_ConSaldoInsuficiente_NoIncrementaContadorDeViajes()
{
    var clock = new FakeClock(new DateTime(2025, 11, 20, 8, 0, 0));
    // cambio: se usa tarjeta en lugar de usofrecuente
    var tarjeta = new Tarjeta(clock);
    var colectivoLocal = new Colectivo("144", clock);
    tarjeta.Cargar(2000);
    // viaje 1 (1580)
    Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
    clock.AdvanceMinutes(10);
    // saldo después: 2000 - 1580 = 420
    // viaje 2 (1580) - pasa a saldo negativo (-1160)
    Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
    clock.AdvanceMinutes(10);
    // saldo después: 420 - 1580 = -1160. contador = 2.
    // viaje 3 (1580) - falla por exceder saldo negativo máximo
    Assert.That(colectivoLocal.PagarCon(tarjeta), Is.False);
    Assert.That(tarjeta.Saldo, Is.EqualTo(-1160).Within(0.01)); // saldo sin cambios. contador = 2.
    tarjeta.Cargar(10000); // saldo: -1160 + 10000 = 8840
    clock.AdvanceMinutes(10);
    // viaje 3 (1580) - éxito
    Assert.That(colectivoLocal.PagarCon(tarjeta), Is.True);
    Assert.That(colectivoLocal.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580).Within(0.01));
    // saldo: 8840 - 1580 = 7260. contador = 3.
    // comprobar que el siguiente viaje (viaje 4) sigue siendo tarifa normal (1580)
    clock.AdvanceMinutes(10);
    Assert.That(tarjeta.ObtenerMontoAPagar(Colectivo.TARIFA_BASICA), Is.EqualTo(1580).Within(0.01));
}

// ==================== TESTS DE TRANSBORDO ====================
/*
//------------------------------------------test transbordo iteracion 5 ------------------------------//
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
//------------------------------------------test transbordo iteracion 5 ------------------------------//
[Test]
public void Trasbordo_TienePrioridadSobreMedioBoleto_CuandoCorresponde()
{
    var clock = new FakeClock(new DateTime(2025, 4, 5, 10, 0, 0)); // día hábil
    var medio = new MedioBoleto(clock);
    medio.Cargar(10000);
    var colectivo144Roja = new Colectivo("144 Roja", clock);
    var colectivo144Negra = new Colectivo("144 Negra", clock);
    // primer viaje: paga medio boleto
    colectivo144Roja.PagarCon(medio);
    Assert.That(colectivo144Roja.UltimoBoleto?.MontoDescontado, Is.EqualTo(790));
    clock.AdvanceMinutes(30);
    // segundo viaje: cumple trasbordo → debe salir gratis, no medio boleto ($790)
    colectivo144Negra.PagarCon(medio);
    Assert.That(colectivo144Negra.UltimoBoleto?.MontoDescontado, Is.EqualTo(0));
    Assert.That(colectivo144Negra.UltimoBoleto?.EsTrasbordo, Is.True);
    Assert.That(colectivo144Negra.UltimoBoleto?.TipoTarjeta, Is.EqualTo("Medio Boleto")); // opcional
}

[Test]
public void Trasbordo_FallaPorTiempo_AplicaMedioBoleto()
{
    var clock = new FakeClock(new DateTime(2025, 4, 5, 10, 0, 0)); // viernes 10:00 (dentro de horario)
    var medio = new MedioBoleto(clock);
    medio.Cargar(10000);
    var colectivoA = new Colectivo("144 Roja", clock);
    var colectivoB = new Colectivo("144 Negra", clock);
    // primer viaje: paga medio boleto
    colectivoA.PagarCon(medio);
    // saldo: 10000 - 10000 - 790 = 9210
    // avanzamos 65 minutos (excede el límite de 1 hora/60 minutos para transbordo)
    clock.AdvanceMinutes(65);
    // segundo viaje: falla transbordo, pero le queda un viaje de medio boleto disponible
    bool pagado = colectivoB.PagarCon(medio);

    // assert
    Assert.That(pagado, Is.True, "el pago debe ser exitoso.");
    Assert.That(colectivoB.UltimoBoleto?.MontoDescontado, Is.EqualTo(790), "debe cobrar medio boleto (790) porque falló el transbordo por tiempo.");
    Assert.That(colectivoB.UltimoBoleto?.EsTrasbordo, Is.False, "no debe ser transbordo.");
    Assert.That(medio.Saldo, Is.EqualTo(9210 - 790).Within(0.01)); // 8420
}
[Test]
public void Trasbordo_FallaPorDiaYHora_AplicaMedioBoleto()
{
    var clock = new FakeClock(new DateTime(2025, 4, 6, 23, 0, 0)); // sábado 23:00 (fuera de horario 7:00-22:00)
    var medio = new MedioBoleto(clock);
    medio.Cargar(10000);
    var colectivoA = new Colectivo("144 Roja", clock);
    var colectivoB = new Colectivo("144 Negra", clock);
    // primer viaje: fuera de horario de franquicia. paga tarifa completa
    colectivoA.PagarCon(medio);
    Assert.That(colectivoA.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580));
    clock.AdvanceMinutes(20);
    // segundo viaje: falla transbordo por hora. falla franquicia por hora. paga tarifa completa
    colectivoB.PagarCon(medio);

    // assert
    Assert.That(colectivoB.UltimoBoleto?.MontoDescontado, Is.EqualTo(1580), "debe cobrar tarifa completa (1580) porque falló transbordo y está fuera de horario de franquicia.");
    Assert.That(colectivoB.UltimoBoleto?.EsTrasbordo, Is.False, "no debe ser transbordo.");
}
*/

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
    var clock = new FakeClock(new DateTime(2025, 4, 5, 23, 0, 0)); //domingo 23:00
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

//================================== MAS TEST CON CASOS EDGES ================================//
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

/*
[Test]
public void Franquicia_FallaAlPagarJustoAntesDeLaHoraDeApertura()
{
    // lunes 5:59:59 (no válido: debe cobrar tarifa normal)
    clock.Now = new DateTime(2025, 4, 7, 5, 59, 59);
    var medio = new MedioBoleto(clock);
    medio.Cargar(5000);
    // act
    bool pagado = colectivo.PagarCon(medio);
    // assert
    Assert.That(pagado, Is.True);
    // debe cobrar la tarifa normal (1580)
    Assert.That(colectivo.UltimoBoleto?.MontoDescontado, Is.EqualTo(TARIFA_BASICA).Within(0.01),
        "debería cobrar tarifa normal justo antes de las 6:00");
}

[Test]
public void Franquicia_FallaAlPagarJustoEnLaHoraDeCierre()
{
    // lunes 22:00:00 (no válido: debe cobrar tarifa normal)
    clock.Now = new DateTime(2025, 4, 7, 22, 1, 0);
    var medio = new MedioBoleto(clock);
    medio.Cargar(5000);
    // act
    bool pagado = colectivo.PagarCon(medio);
    // assert
    Assert.That(pagado, Is.True);
    // debe cobrar la tarifa normal (1580)
    Assert.That(colectivo.UltimoBoleto?.MontoDescontado, Is.EqualTo(TARIFA_BASICA).Within(0.01),
        "debería cobrar tarifa normal justo a las 22:00");
}

[Test]
public void Franquicia_FallaAlPagarEnFinDeSemana_EnHorarioValido()
{
    // sábado 10:00:00 (no válido por ser fin de semana)
    clock.Now = new DateTime(2025, 4, 5, 10, 0, 0);
    var medio = new MedioBoleto(clock);
    medio.Cargar(5000);
    // act
    bool pagado = colectivo.PagarCon(medio);
    // assert
    Assert.That(pagado, Is.True);
    // debe cobrar la tarifa normal (1580)
    Assert.That(colectivo.UltimoBoleto?.MontoDescontado, Is.EqualTo(TARIFA_BASICA).Within(0.01),
        "debería cobrar tarifa normal en fin de semana");
}
*/

[Test]
public void Trasbordo_FallaSiSeIntentaEnLaMismaLinea()
{
    var clock = new FakeClock(new DateTime(2025, 4, 7, 10, 0, 0)); //lunes
    var tarjeta = new Tarjeta(clock);
    tarjeta.Cargar(10000);
    var colectivoA = new Colectivo("144 Negra", clock);
    //Primer viaje: paga normal (1580)
    Assert.That(colectivoA.PagarCon(tarjeta), Is.True);
    Assert.That(colectivoA.UltimoBoleto?.MontoDescontado, Is.EqualTo(TARIFA_BASICA));
    clock.AdvanceMinutes(20); //tiempo válido para trasbordo
                              //segundo viaje en la misma línea: debe fallar el trasbordo y cobrar normal
    Assert.That(colectivoA.PagarCon(tarjeta), Is.True);
    Assert.That(colectivoA.UltimoBoleto?.MontoDescontado, Is.EqualTo(TARIFA_BASICA));
    Assert.That(colectivoA.UltimoBoleto?.EsTrasbordo, Is.False,
        "no debe aplicar trasbordo en la misma línea");
}

[Test]
public void Trasbordo_FallaSiExcedeElLimiteDeUnaHora()
{
    var clock = new FakeClock(new DateTime(2025, 4, 7, 10, 0, 0)); // lunes
    var tarjeta = new Tarjeta(clock);
    tarjeta.Cargar(10000);
    var colectivoA = new Colectivo("144 Negra", clock);
    var colectivoB = new Colectivo("144 Roja", clock);
    //primer viaje: paga normal (1580)
    Assert.That(colectivoA.PagarCon(tarjeta), Is.True);
    Assert.That(colectivoA.UltimoBoleto?.MontoDescontado, Is.EqualTo(TARIFA_BASICA));
    //avanzamos 60 minutos y 1 segundo (excede el límite)
    clock.AdvanceTime(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));
    //segundo viaje en otra línea: debe fallar el trasbordo y cobrar normal
    Assert.That(colectivoB.PagarCon(tarjeta), Is.True);
    Assert.That(colectivoB.UltimoBoleto?.MontoDescontado, Is.EqualTo(TARIFA_BASICA));
    Assert.That(colectivoB.UltimoBoleto?.EsTrasbordo, Is.False,
        "no debe aplicar trasbordo si excede el límite de 1 hora");
}
}
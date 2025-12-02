using System;
using System.Collections.Generic;
using NUnit.Framework;
using TarjetaSube;

namespace TarjetaSubeTest
{
    [TestFixture]
    public class MiBiciTuBiciTests
    {
        private FakeClock _clock;
        private MiBiciTuBici _miBiciTuBici;

        [SetUp]
        public void Setup()
        {
            _clock = new FakeClock(new DateTime(2024, 1, 1, 10, 0, 0));
            _miBiciTuBici = new MiBiciTuBici(_clock);
        }

        //----------------------------------------------------------------------------------------------------------------//
        //Se paga la tarifa y se retira una bici exitosamente. Verificando que la tarifa cobrada sea la correcta,
        //sin demora en la devolución y con el saldo suficiente en la tarjeta.

        [Test]
        public void SePagaLaTarifaYSeRetiraUnaBiciExitosamente_VerificandoTarifaCorrecta()
        {
            var tarjeta = new Tarjeta();
            tarjeta.Cargar(5000);

            bool resultado = _miBiciTuBici.PagarCon(tarjeta);

            Assert.IsTrue(resultado, "El pago debería ser exitoso");
            Assert.IsNotNull(_miBiciTuBici.UltimoBoleto, "Debería generarse un boleto");
            Assert.AreEqual(MiBiciTuBici.TARIFA_DIARIA, _miBiciTuBici.UltimoBoleto!.MontoPagado, "La tarifa cobrada debe ser la correcta");
            Assert.AreEqual(5000 - MiBiciTuBici.TARIFA_DIARIA, _miBiciTuBici.UltimoBoleto.SaldoRestante, 0.01, "El saldo restante debe ser correcto");
            Assert.IsFalse(_miBiciTuBici.UltimoBoleto.HuboMulta, "No debería haber multa en el primer retiro");
            Assert.AreEqual(0, _miBiciTuBici.UltimoBoleto.MultasAplicadas, "No debería haber multas aplicadas");
        }

        //----------------------------------------------------------------------------------------------------------------//
        //No se puede retirar la bici. Validando que esto suceda cuando no hay suficiente saldo en la tarjeta teniendo o no una multa pendiente de pagar.
        [Test]
        public void NoSePuedeRetirarLaBici_CuandoNoHaySuficienteSaldo_SinMultaPendiente()
        {

            var tarjeta = new Tarjeta();

            bool resultado = _miBiciTuBici.PagarCon(tarjeta);

            Assert.IsFalse(resultado, "El pago debería fallar por saldo insuficiente");
            Assert.IsNull(_miBiciTuBici.UltimoBoleto, "No debería generarse un boleto");
        }

        [Test]
        public void NoSePuedeRetirarLaBici_CuandoNoHaySuficienteSaldo_ConMultaPendiente()
        {

            var tarjeta = new Tarjeta();
            tarjeta.Cargar(2000); //saldo justo para un retiro pero no para multas

            //primer retiro exitoso (2000 - 1777.5 = 222.5)
            bool primerResultado = _miBiciTuBici.PagarCon(tarjeta);
            Assert.IsTrue(primerResultado, "Primer retiro debería ser exitoso");

            //avanzamos 2.5 horas (150 minutos) - genera 1 multa (120-150 minutos = 1 multa)
            _clock.AdvanceMinutes(150);

            //devolvemos para registrar la multa pendiente
            _miBiciTuBici.DevolvioLaBici(tarjeta);

            //verificar multas pendientes ANTES del pago
            int multasAntes = _miBiciTuBici.ObtenerMultasPendientes(tarjeta);
            Assert.AreEqual(1, multasAntes, "Debería haber 1 multa pendiente con 150 minutos");

            //segundo retiro con 1 multa pendiente
            //monto a pagar: 1777.5 (tarifa) + 1000 (1 multa) = 2777.5
            //saldo actual: 222.5
            //222.5 - 2777.5 = -2555 (excede el límite de -1200)
            bool segundoResultado = _miBiciTuBici.PagarCon(tarjeta);

            Assert.IsFalse(segundoResultado, "El segundo pago debería fallar por saldo insuficiente con multas");

            //verificar que las multas pendientes se mantienen después del pago fallido
            int multasDespues = _miBiciTuBici.ObtenerMultasPendientes(tarjeta);
            Assert.AreEqual(1, multasDespues, "Debería mantener 1 multa pendiente después del pago fallido");
        }

        //----------------------------------------------------------------------------------------------------------------//
        //Se retira una bici con una o varias multas acumuladas.
        [Test]
        public void SeRetiraUnaBici_ConUnaMultaAcumulada()
        {

            var tarjeta = new Tarjeta();
            tarjeta.Cargar(5000);

            bool primerResultado = _miBiciTuBici.PagarCon(tarjeta);
            Assert.IsTrue(primerResultado, "Primer retiro debería ser exitoso");

            //avanzamos mas de 2 horas (150 minutos) - genera 1 multa (121-180 minutos = 1 multa)
            _clock.AdvanceMinutes(150);

            //devolvemos para registrar la multa
            _miBiciTuBici.DevolvioLaBici(tarjeta);

            //segundo retiro con 1 multa pendiente
            bool segundoResultado = _miBiciTuBici.PagarCon(tarjeta);

            Assert.IsTrue(segundoResultado, "El segundo retiro debería ser exitoso con multa");
            Assert.IsNotNull(_miBiciTuBici.UltimoBoleto, "Debería generarse un boleto");
            Assert.AreEqual(MiBiciTuBici.TARIFA_DIARIA + MiBiciTuBici.MULTA_POR_EXCESO, _miBiciTuBici.UltimoBoleto!.MontoPagado, "Debería cobrarse tarifa + 1 multa");
            Assert.IsTrue(_miBiciTuBici.UltimoBoleto.HuboMulta, "Debería indicar que hubo multa");
            Assert.AreEqual(1, _miBiciTuBici.UltimoBoleto.MultasAplicadas, "Debería tener 1 multa aplicada");
        }

        [Test]
        public void SeRetiraUnaBici_ConVariasMultasAcumuladas()
        {

            var tarjeta = new Tarjeta();
            tarjeta.Cargar(10000);

            //primer retiro
            bool primerResultado = _miBiciTuBici.PagarCon(tarjeta);
            Assert.IsTrue(primerResultado, "Primer retiro debería ser exitoso");

            //avanzamos 4.5 horas (270 minutos) - genera 3 multas 
            //(121-180 = 1 multa, 181-240 = 2 multas, 241-270 = 3 multas)
            _clock.AdvanceMinutes(270);

            //devolvemos para registrar las multas
            _miBiciTuBici.DevolvioLaBici(tarjeta);

            //segundo retiro con 3 multas pendientes
            bool segundoResultado = _miBiciTuBici.PagarCon(tarjeta);

            Assert.IsTrue(segundoResultado, "El segundo retiro debería ser exitoso con multas");
            Assert.IsNotNull(_miBiciTuBici.UltimoBoleto, "Debería generarse un boleto");
            double montoEsperado = MiBiciTuBici.TARIFA_DIARIA + (3 * MiBiciTuBici.MULTA_POR_EXCESO);
            Assert.AreEqual(montoEsperado, _miBiciTuBici.UltimoBoleto!.MontoPagado, "Debería cobrarse tarifa + 3 multas");
            Assert.IsTrue(_miBiciTuBici.UltimoBoleto.HuboMulta, "Debería indicar que hubo multa");
            Assert.AreEqual(3, _miBiciTuBici.UltimoBoleto.MultasAplicadas, "Debería tener 3 multas aplicadas");
        }

        // ===================================== TESTS EXTRAS ================================= //
        [Test]
        public void SeRetiraBici_ConMedioBoleto_PagaTarifaCompleta()
        {

            var tarjeta = new MedioBoleto(_clock);
            tarjeta.Cargar(5000);

            bool resultado = _miBiciTuBici.PagarCon(tarjeta);

            Assert.IsTrue(resultado, "El pago debería ser exitoso");
            Assert.IsNotNull(_miBiciTuBici.UltimoBoleto, "Debería generarse un boleto");
            Assert.AreEqual(MiBiciTuBici.TARIFA_DIARIA, _miBiciTuBici.UltimoBoleto!.MontoPagado, "Debería pagar tarifa completa aunque sea MedioBoleto");
            Assert.AreEqual("Medio Boleto", _miBiciTuBici.UltimoBoleto.TipoTarjeta, "Debería reconocer el tipo de tarjeta");
        }

        [Test]
        public void SeRetiraBici_ConBoletoGratuito_PagaTarifaCompleta()
        {

            var tarjeta = new BoletoGratuito(_clock);
            tarjeta.Cargar(5000);

            bool resultado = _miBiciTuBici.PagarCon(tarjeta);

            Assert.IsTrue(resultado, "El pago debería ser exitoso");
            Assert.IsNotNull(_miBiciTuBici.UltimoBoleto, "Debería generarse un boleto");
            Assert.AreEqual(MiBiciTuBici.TARIFA_DIARIA, _miBiciTuBici.UltimoBoleto!.MontoPagado, "Debería pagar tarifa completa aunque sea BoletoGratuito");
            Assert.AreEqual("Boleto Gratuito", _miBiciTuBici.UltimoBoleto.TipoTarjeta, "Debería reconocer el tipo de tarjeta");
        }

        [Test]
        public void MultasNoSeReseteanAlDiaSiguiente()
        {

            var tarjeta = new Tarjeta();
            tarjeta.Cargar(10000);

            _miBiciTuBici.PagarCon(tarjeta);

            //avanzamos mas de 2 horas (150 minutos) - genera 1 multa
            _clock.AdvanceMinutes(150);

            //devolvemos la bici (esto registra 1 multa pendiente)
            _miBiciTuBici.DevolvioLaBici(tarjeta);

            //verificar multas pendientes
            int multasPendientes = _miBiciTuBici.ObtenerMultasPendientes(tarjeta);
            Assert.AreEqual(1, multasPendientes, "Debería haber 1 multa pendiente por la devolución tardía");

            //avanzamos al día siguiente
            _clock.AdvanceDays(1);

            //segundo retiro al día siguiente (debe cobrar multa pendiente)
            bool resultado = _miBiciTuBici.PagarCon(tarjeta);

            Assert.IsTrue(resultado, "El retiro debería ser exitoso");
            double montoEsperado = MiBiciTuBici.TARIFA_DIARIA + MiBiciTuBici.MULTA_POR_EXCESO;
            Assert.AreEqual(montoEsperado, _miBiciTuBici.UltimoBoleto!.MontoPagado, "Debería cobrar tarifa + 1 multa pendiente");
            Assert.IsTrue(_miBiciTuBici.UltimoBoleto.HuboMulta, "Debería indicar que hubo multa");
            Assert.AreEqual(1, _miBiciTuBici.UltimoBoleto.MultasAplicadas, "Debería tener 1 multa aplicada");

            //verificar que ya no hay multas pendientes después del pago
            int multasDespues = _miBiciTuBici.ObtenerMultasPendientes(tarjeta);
            Assert.AreEqual(0, multasDespues, "No debería haber multas pendientes después del pago");
        }

        [Test]
        public void SePuedeRetirarBici_ConFranquiciaCompleta_PeroPagaTarifaCompleta()
        {

            var tarjeta = new FranquiciaCompleta();

            bool resultado = _miBiciTuBici.PagarCon(tarjeta);

            Assert.IsTrue(resultado, "El pago debería ser exitoso con FranquiciaCompleta");
            Assert.IsNotNull(_miBiciTuBici.UltimoBoleto, "Debería generarse un boleto");
            Assert.AreEqual("Franquicia Completa", _miBiciTuBici.UltimoBoleto!.TipoTarjeta, "Debería reconocer el tipo de tarjeta");
        }
        /*
        [Test]
        public void HistorialMultas_DevuelveFechasCorrectas()
        {

            var tarjeta = new Tarjeta();
            tarjeta.Cargar(10000);

            //primer retiro y devolución con multa
            _miBiciTuBici.PagarCon(tarjeta);
            _clock.AdvanceMinutes(180); //3 horas - 1 multa
            _miBiciTuBici.DevolvioLaBici(tarjeta); //registra 1 multa

            //segundo retiro y devolución con multa
            _clock.AdvanceMinutes(60);
            _miBiciTuBici.PagarCon(tarjeta);
            _clock.AdvanceMinutes(240); //4 horas - 2 multas
            _miBiciTuBici.DevolvioLaBici(tarjeta); //registra 2 multas

            var historial = _miBiciTuBici.ObtenerHistorialMultas(tarjeta);

            Assert.AreEqual(2, historial.Count, "Debería haber 2 fechas en el historial de multas");
        }
        */
        [Test]
        public void TestMultasExactas()
        {
            //caso 1: 60 minutos = 0 multas
            var tarjeta1 = new Tarjeta();
            tarjeta1.Cargar(10000);
            _miBiciTuBici.PagarCon(tarjeta1);
            _clock.AdvanceMinutes(60);
            _miBiciTuBici.DevolvioLaBici(tarjeta1);
            Assert.AreEqual(0, _miBiciTuBici.ObtenerMultasPendientes(tarjeta1), "60 minutos = 0 multas");

            //reset para el siguiente test
            _clock = new FakeClock(new DateTime(2024, 1, 1, 10, 0, 0));
            _miBiciTuBici = new MiBiciTuBici(_clock);

            //caso 2: 119 minutos = 0 multas
            var tarjeta2 = new Tarjeta();
            tarjeta2.Cargar(10000);
            _miBiciTuBici.PagarCon(tarjeta2);
            _clock.AdvanceMinutes(119);
            _miBiciTuBici.DevolvioLaBici(tarjeta2);
            Assert.AreEqual(0, _miBiciTuBici.ObtenerMultasPendientes(tarjeta2), "119 minutos = 0 multas");

            //reset para el siguiente test
            _clock = new FakeClock(new DateTime(2024, 1, 1, 10, 0, 0));
            _miBiciTuBici = new MiBiciTuBici(_clock);

            //caso 3: 120 minutos = 1 multa
            var tarjeta3 = new Tarjeta();
            tarjeta3.Cargar(10000);
            _miBiciTuBici.PagarCon(tarjeta3);
            _clock.AdvanceMinutes(120);
            _miBiciTuBici.DevolvioLaBici(tarjeta3);
            Assert.AreEqual(1, _miBiciTuBici.ObtenerMultasPendientes(tarjeta3), "120 minutos = 1 multa");

            //reset para el siguiente test
            _clock = new FakeClock(new DateTime(2024, 1, 1, 10, 0, 0));
            _miBiciTuBici = new MiBiciTuBici(_clock);

            //caso 4: 179 minutos = 1 multa
            var tarjeta4 = new Tarjeta();
            tarjeta4.Cargar(10000);
            _miBiciTuBici.PagarCon(tarjeta4);
            _clock.AdvanceMinutes(179);
            _miBiciTuBici.DevolvioLaBici(tarjeta4);
            Assert.AreEqual(1, _miBiciTuBici.ObtenerMultasPendientes(tarjeta4), "179 minutos = 1 multa");

            //reset para el siguiente test
            _clock = new FakeClock(new DateTime(2024, 1, 1, 10, 0, 0));
            _miBiciTuBici = new MiBiciTuBici(_clock);

            //caso 5: 180 minutos = 2 multas
            var tarjeta5 = new Tarjeta();
            tarjeta5.Cargar(10000);
            _miBiciTuBici.PagarCon(tarjeta5);
            _clock.AdvanceMinutes(180);
            _miBiciTuBici.DevolvioLaBici(tarjeta5);
            Assert.AreEqual(2, _miBiciTuBici.ObtenerMultasPendientes(tarjeta5), "180 minutos = 2 multas");

            //reset para el siguiente test
            _clock = new FakeClock(new DateTime(2024, 1, 1, 10, 0, 0));
            _miBiciTuBici = new MiBiciTuBici(_clock);

            //caso 6: 239 minutos = 2 multas
            var tarjeta6 = new Tarjeta();
            tarjeta6.Cargar(10000);
            _miBiciTuBici.PagarCon(tarjeta6);
            _clock.AdvanceMinutes(239);
            _miBiciTuBici.DevolvioLaBici(tarjeta6);
            Assert.AreEqual(2, _miBiciTuBici.ObtenerMultasPendientes(tarjeta6), "239 minutos = 2 multas");

            //reset para el siguiente test
            _clock = new FakeClock(new DateTime(2024, 1, 1, 10, 0, 0));
            _miBiciTuBici = new MiBiciTuBici(_clock);

            //caso 7: 240 minutos = 3 multas
            var tarjeta7 = new Tarjeta();
            tarjeta7.Cargar(10000);
            _miBiciTuBici.PagarCon(tarjeta7);
            _clock.AdvanceMinutes(240);
            _miBiciTuBici.DevolvioLaBici(tarjeta7);
            Assert.AreEqual(3, _miBiciTuBici.ObtenerMultasPendientes(tarjeta7), "240 minutos = 3 multas");

            //reset para el siguiente test
            _clock = new FakeClock(new DateTime(2024, 1, 1, 10, 0, 0));
            _miBiciTuBici = new MiBiciTuBici(_clock);

            //caso 8: 250 minutos = 3 multas
            var tarjeta8 = new Tarjeta();
            tarjeta8.Cargar(10000);
            _miBiciTuBici.PagarCon(tarjeta8);
            _clock.AdvanceMinutes(250);
            _miBiciTuBici.DevolvioLaBici(tarjeta8);
            Assert.AreEqual(3, _miBiciTuBici.ObtenerMultasPendientes(tarjeta8), "250 minutos = 3 multas");

            //reset para el siguiente test
            _clock = new FakeClock(new DateTime(2024, 1, 1, 10, 0, 0));
            _miBiciTuBici = new MiBiciTuBici(_clock);

            //caso 9: 270 minutos = 3 multas
            var tarjeta9 = new Tarjeta();
            tarjeta9.Cargar(10000);
            _miBiciTuBici.PagarCon(tarjeta9);
            _clock.AdvanceMinutes(270);
            _miBiciTuBici.DevolvioLaBici(tarjeta9);
            Assert.AreEqual(3, _miBiciTuBici.ObtenerMultasPendientes(tarjeta9), "270 minutos = 3 multas");

            //reset para el siguiente test
            _clock = new FakeClock(new DateTime(2024, 1, 1, 10, 0, 0));
            _miBiciTuBici = new MiBiciTuBici(_clock);

            //caso 10: 300 minutos = 4 multas
            var tarjeta10 = new Tarjeta();
            tarjeta10.Cargar(10000);
            _miBiciTuBici.PagarCon(tarjeta10);
            _clock.AdvanceMinutes(300);
            _miBiciTuBici.DevolvioLaBici(tarjeta10);
            Assert.AreEqual(4, _miBiciTuBici.ObtenerMultasPendientes(tarjeta10), "300 minutos = 4 multas");
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
    }
}
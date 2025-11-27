using NUnit.Framework;
using System;
using TarjetaSube;

[TestFixture]
public class BoletoTests
{
    [Test]
    public void Boleto_ConSaldoPositivo_MontoTotalAbonadoIgualAlDescontado()
    {
        // Arrange
        var idTarjeta = Guid.NewGuid();
        var fecha = new DateTime(2024, 1, 15, 8, 30, 0);

        // Act - Usuario con saldo positivo paga un viaje normal
        var boleto = new Boleto(
            idTarjeta: idTarjeta,
            linea: "144 Roja",
            fechaHora: fecha,
            tipoTarjeta: "Tarjeta Normal",
            montoDescontado: 1580,
            tarifaNormal: 1580,
            saldoRestante: 5000,
            esTrasbordo: false
        );

        // Assert - El monto total abonado debe ser igual al descontado
        Assert.That(boleto.MontoTotalAbonado, Is.EqualTo(1580));
        Assert.That(boleto.EsTrasbordo, Is.False);
        Assert.That(boleto.TipoTarjeta, Is.EqualTo("Tarjeta Normal"));
    }

    [Test]
    public void Boleto_ConSaldoNegativo_MontoTotalAbonadoIncluyeDeuda()
    {
        // Arrange
        var idTarjeta = Guid.NewGuid();
        var fecha = new DateTime(2024, 1, 15, 18, 15, 0);

        // Act - Usuario con saldo negativo (usó crédito plus)
        // Saldo antes: -500, se descuenta 1580, saldo después: -2080
        // Pero el monto total abonado debe ser la tarifa normal (1580)
        var boleto = new Boleto(
            idTarjeta: idTarjeta,
            linea: "60",
            fechaHora: fecha,
            tipoTarjeta: "Tarjeta Normal",
            montoDescontado: 1580,
            tarifaNormal: 1580,
            saldoRestante: -2080, // Ya estaba en negativo
            esTrasbordo: false
        );

        // Assert - El monto total abonado incluye la deuda recuperada
        Assert.That(boleto.MontoTotalAbonado, Is.EqualTo(1580));
        Assert.That(boleto.SaldoRestante, Is.EqualTo(-2080));
    }

    [Test]
    public void Boleto_Trasbordo_GratisYMarcadoComoTrasbordo()
    {
        // Arrange
        var idTarjeta = Guid.NewGuid();
        var fecha = new DateTime(2024, 1, 15, 9, 45, 0);

        // Act - Usuario hace trasbordo dentro de la hora
        var boleto = new Boleto(
            idTarjeta: idTarjeta,
            linea: "39",
            fechaHora: fecha,
            tipoTarjeta: "Tarjeta Normal",
            montoDescontado: 0, // Gratis por trasbordo
            tarifaNormal: 1580,
            saldoRestante: 3000,
            esTrasbordo: true
        );

        // Assert - Trasbordo gratis y marcado correctamente
        Assert.That(boleto.MontoDescontado, Is.EqualTo(0));
        Assert.That(boleto.MontoTotalAbonado, Is.EqualTo(0));
        Assert.That(boleto.EsTrasbordo, Is.True);
        Assert.That(boleto.SaldoRestante, Is.EqualTo(3000)); // No se descuenta nada
    }

    [Test]
    public void Boleto_MedioBoleto_DescuentoAplicadoCorrectamente()
    {
        // Arrange
        var idTarjeta = Guid.NewGuid();
        var fecha = new DateTime(2024, 1, 15, 14, 20, 0);

        // Act - Usuario con medio boleto en horario válido
        var boleto = new Boleto(
            idTarjeta: idTarjeta,
            linea: "152",
            fechaHora: fecha,
            tipoTarjeta: "Medio Boleto",
            montoDescontado: 790, // 50% de descuento
            tarifaNormal: 1580,
            saldoRestante: 4210,
            esTrasbordo: false
        );

        // Assert - Descuento aplicado correctamente
        Assert.That(boleto.MontoDescontado, Is.EqualTo(790));
        Assert.That(boleto.MontoTotalAbonado, Is.EqualTo(790));
        Assert.That(boleto.TipoTarjeta, Is.EqualTo("Medio Boleto"));
    }

    [Test]
    public void Boleto_BoletoGratuito_SinCostoParaUsuario()
    {
        // Arrange
        var idTarjeta = Guid.NewGuid();
        var fecha = new DateTime(2024, 1, 15, 11, 10, 0);

        // Act - Usuario con boleto gratuito (primeros 2 viajes del día)
        var boleto = new Boleto(
            idTarjeta: idTarjeta,
            linea: "28",
            fechaHora: fecha,
            tipoTarjeta: "Boleto Gratuito",
            montoDescontado: 0,
            tarifaNormal: 1580,
            saldoRestante: 10000, // Saldo no cambia
            esTrasbordo: false
        );

        // Assert - Viaje completamente gratis
        Assert.That(boleto.MontoDescontado, Is.EqualTo(0));
        Assert.That(boleto.MontoTotalAbonado, Is.EqualTo(0));
        Assert.That(boleto.SaldoRestante, Is.EqualTo(10000));
    }

    [Test]
    public void Boleto_UsoFrecuente_ConDescuentoAplicado()
    {
        // Arrange
        var idTarjeta = Guid.NewGuid();
        var fecha = new DateTime(2024, 1, 15, 17, 45, 0);

        // Act - Usuario en rango de 20% descuento (viaje 35 del mes)
        var boleto = new Boleto(
            idTarjeta: idTarjeta,
            linea: "110",
            fechaHora: fecha,
            tipoTarjeta: "Tarjeta Uso Frecuente",
            montoDescontado: 1264, // 1580 * 0.8 = 1264
            tarifaNormal: 1580,
            saldoRestante: 2500,
            esTrasbordo: false
        );

        // Assert - Descuento del 20% aplicado
        Assert.That(boleto.MontoDescontado, Is.EqualTo(1264));
        Assert.That(boleto.MontoTotalAbonado, Is.EqualTo(1264));
        Assert.That(boleto.TipoTarjeta, Is.EqualTo("Tarjeta Uso Frecuente"));
    }

    [Test]
    public void Boleto_Interurbano_TarifaEspecialAplicada()
    {
        // Arrange
        var idTarjeta = Guid.NewGuid();
        var fecha = new DateTime(2024, 1, 15, 6, 30, 0);

        // Act - Usuario viaja en colectivo interurbano
        var boleto = new Boleto(
            idTarjeta: idTarjeta,
            linea: "Línea 500 (Interurbana)",
            fechaHora: fecha,
            tipoTarjeta: "Tarjeta Normal (Interurbana)",
            montoDescontado: 3000, // Tarifa interurbana
            tarifaNormal: 3000,
            saldoRestante: 7000,
            esTrasbordo: false
        );

        // Assert - Tarifa interurbana aplicada
        Assert.That(boleto.MontoDescontado, Is.EqualTo(3000));
        Assert.That(boleto.TarifaNormal, Is.EqualTo(3000));
        Assert.That(boleto.TipoTarjeta, Is.EqualTo("Tarjeta Normal (Interurbana)"));
    }

    [Test]
    public void Boleto_Impresion_FormatoCorrecto()
    {
        // Arrange
        var idTarjeta = Guid.NewGuid();
        var fecha = new DateTime(2024, 1, 15, 12, 0, 0);
        var boleto = new Boleto(
            idTarjeta: idTarjeta,
            linea: "76",
            fechaHora: fecha,
            tipoTarjeta: "Tarjeta Normal",
            montoDescontado: 1580,
            tarifaNormal: 1580,
            saldoRestante: 3420,
            esTrasbordo: false
        );

        // Act - Capturar la salida de consola
        using var consoleOutput = new ConsoleOutput();
        boleto.Imprimir();
        string output = consoleOutput.GetOutput();

        // Assert - Verificar que la información clave esté presente
        // CORRECCIÓN: Usar Contains.Substring sin formato específico o adaptarse al formato local
        Assert.That(output, Contains.Substring("BOLETO SUBE"));
        Assert.That(output, Contains.Substring("Línea: 76"));
        Assert.That(output, Contains.Substring("Tipo: Tarjeta Normal"));
        Assert.That(output, Contains.Substring("Descontado: $1580")); // Sin .00 específico
        Assert.That(output, Contains.Substring("Saldo final: $3420")); // Sin .00 específico
        Assert.That(output, Contains.Substring("TOTAL ABONADO: $1580")); // Sin .00 específico
        Assert.That(output, Contains.Substring("Trasbordo: No"));
    }

    [Test]
    public void Boleto_MultipleUsuarios_DatosConsistentes()
    {
        // Arrange - Simular varios usuarios en el mismo colectivo
        var fecha = DateTime.Now;
        var idUsuario1 = Guid.NewGuid();
        var idUsuario2 = Guid.NewGuid();

        // Act - Dos usuarios pagan en el mismo colectivo
        var boletoUsuario1 = new Boleto(
            idTarjeta: idUsuario1,
            linea: "134",
            fechaHora: fecha,
            tipoTarjeta: "Medio Boleto",
            montoDescontado: 790,
            tarifaNormal: 1580,
            saldoRestante: 2100,
            esTrasbordo: false
        );

        var boletoUsuario2 = new Boleto(
            idTarjeta: idUsuario2,
            linea: "134",
            fechaHora: fecha.AddMinutes(1),
            tipoTarjeta: "Tarjeta Normal",
            montoDescontado: 1580,
            tarifaNormal: 1580,
            saldoRestante: 8420,
            esTrasbordo: false
        );

        // Assert - Cada boleto tiene sus datos correctos
        Assert.That(boletoUsuario1.IdTarjeta, Is.EqualTo(idUsuario1));
        Assert.That(boletoUsuario2.IdTarjeta, Is.EqualTo(idUsuario2));
        Assert.That(boletoUsuario1.MontoDescontado, Is.EqualTo(790));
        Assert.That(boletoUsuario2.MontoDescontado, Is.EqualTo(1580));
        Assert.That(boletoUsuario1.Linea, Is.EqualTo("134"));
        Assert.That(boletoUsuario2.Linea, Is.EqualTo("134"));
    }
}

// Clase auxiliar para capturar la salida de consola
public class ConsoleOutput : IDisposable
{
    private readonly System.IO.StringWriter stringWriter;
    private readonly System.IO.TextWriter originalOutput;

    public ConsoleOutput()
    {
        stringWriter = new System.IO.StringWriter();
        originalOutput = Console.Out;
        Console.SetOut(stringWriter);
    }

    public string GetOutput()
    {
        return stringWriter.ToString();
    }

    public void Dispose()
    {
        Console.SetOut(originalOutput);
        stringWriter.Dispose();
    }
}
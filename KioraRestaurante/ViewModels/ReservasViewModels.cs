using KioraRestaurante.DTOs.Reserva;
using KioraRestaurante.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
namespace KioraRestaurante.ViewModels;
public class ReservaFormViewModel
{
    public CriarReservaDTO Dados { get; set; } = new() {ChaveSolicitacao=Guid.NewGuid()};
    [BindNever,ValidateNever] public List<HorarioReservaDTO> Horarios { get; set; } = new();
    [BindNever,ValidateNever] public DateOnly Hoje { get; set; }
    [BindNever,ValidateNever] public DateOnly Limite { get; set; }
}
public class ReservasAdminViewModel
{
    public DateOnly Data { get; set; }
    public List<MesaRestaurante> Mesas { get; set; } = new();
    public List<Reserva> Reservas { get; set; } = new();
}

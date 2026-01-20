using HojaDeRuta.Controllers;
using HojaDeRuta.Models.Config;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Services.Repository;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using System;
using System.Linq.Expressions;

namespace HojaDeRuta.Services
{
    public class RevisorService
    {
        private readonly ILogger<RevisorService> _logger;
        private readonly IGenericRepository<Revisores> revisoresRepository;
        private readonly DBSettings dbSettings;

        public RevisorService(
            ILogger<RevisorService> logger,
            IGenericRepository<Revisores> revisoresRepository,
            IOptions<DBSettings> dbSettings
            )
        {
            _logger = logger;
            this.revisoresRepository = revisoresRepository;
            this.dbSettings = dbSettings.Value;
        }

        public async Task<List<Revisores>> GetAllRevisores()
        {
            try
            {
                IEnumerable<Revisores> revisores = await revisoresRepository.GetAllAsync();
                return revisores.OrderBy(r => r.Detalle).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<Revisores> GetRevisorByName(string name)
        {
            _logger.LogInformation($"Armado del predicado para GetRevisorByName " +
                $"para el empleado {name}");

            Expression<Func<Revisores, bool>> revisor = s => s.Empleado == name;

            var revisores = await revisoresRepository.FindAsync(revisor);

            return revisores.FirstOrDefault();
        }

        public async Task<List<Revisores>> GetRevisoresByNivel(Dictionary<string, int> parameters)
        {
            try
            {
                var spName = dbSettings.Sp["GetRevisoresByNivel"].ToString();

                IEnumerable<Revisores> revisores = await revisoresRepository.ExecuteStoredProcedureAsync(spName, parameters);
                return revisores.OrderBy(r => r.Detalle).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<string> GetCampoHabilitado(Hoja hoja, bool obtenerAnterior = false)
        {
            ////PARA TEST
            //hoja.Preparo = "0";
            //hoja.Reviso = "1";
            //hoja.RevisionGerente = "";
            //hoja.EngagementPartner = "3";
            //hoja.SocioFirmante = "5";
            //obtenerAnterior = false;

            var pasosFlujo = new List<(string Nombre, string Valor)>
            {
                ("Preparo", hoja.Preparo),
                ("Reviso", hoja.Reviso),
                ("RevisionGerente", hoja.RevisionGerente),
                ("EngagementPartner", hoja.EngagementPartner),
                ("SocioFirmante", hoja.SocioFirmante)
            };

            for (int i = 0; i < pasosFlujo.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(pasosFlujo[i].Valor))
                {
                    if (obtenerAnterior && i > 0)
                    {
                        return pasosFlujo[i - 1].Nombre;
                    }

                    return pasosFlujo[i].Nombre;
                }
            }

            return pasosFlujo.Last().Nombre;
        }

        public async Task<int> GetNivelRevisorActual(Hoja hoja)
        {
            string CampoAnterior = await GetCampoHabilitado(hoja, true);

            var revisor = typeof(Hoja)
                    .GetProperty(CampoAnterior)
                    ?.GetValue(hoja, null)?.ToString();

            Revisores revisorActual = await GetRevisorByName(revisor);

            //return revisorActual.Cargo.Value;
            return revisorActual?.Cargo ?? 0;
        }

        public async Task<Revisores> GetRevisorActual(Hoja hoja)
        {
            try
            {
                string campoRevisorActual = await GetCampoHabilitado(hoja, true);

                string? revisorActual = hoja.GetType().
                                    GetProperty(campoRevisorActual)?
                                    .GetValue(hoja, null)?.ToString();

                if (String.IsNullOrWhiteSpace(revisorActual))
                {
                    throw new Exception($"No se pudo encontrar el revisor" +
                        $" para el campo {campoRevisorActual}");
                }

                return await GetRevisorByName(revisorActual);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<List<Revisores>> GetRevisoresParaNotificar (Hoja hoja,
                                string revisorActual, bool buscarAnterior = false)
        {
            var resultado = new List<Revisores>();

            try
            {
                _logger.LogInformation($"Obtener {(buscarAnterior ? "revisores anteriores" : "próximo revisor")} para la hoja {hoja.Id}. Revisor actual: {revisorActual}");

                var pasosFlujo = new List<(string Nombre, string Valor)>
                {
                    ("Preparo", hoja.Preparo),
                    ("Reviso", hoja.Reviso),
                    ("RevisionGerente", hoja.RevisionGerente),
                    ("EngagementPartner", hoja.EngagementPartner),
                    ("SocioFirmante", hoja.SocioFirmante)
                };

                int indexActual = pasosFlujo.FindIndex(p => p.Valor == revisorActual);

                if (indexActual == -1)
                {
                    _logger.LogError($"No se encontró el revisor actual ({revisorActual}) en los pasos de flujo de la hoja {hoja.Id}.");
                    return resultado;
                }

                IEnumerable<(string Nombre, string Valor)> revisoresRelacionados;

                if (buscarAnterior)
                {
                    _logger.LogInformation($"Se buscarán los revisores anteriores");

                    revisoresRelacionados = pasosFlujo
                        .Take(indexActual)
                        .Where(p => !string.IsNullOrEmpty(p.Valor))
                        .Reverse()
                        .ToList();

                    string revisoresConcatenados = string.Join("-", revisoresRelacionados.Select(p => p.Valor));

                    string revisoresLogText = revisoresRelacionados.Count() > 0
                        ? $"Se obtuvieron los siguientes revisores relacionados: {revisoresConcatenados}" +
                                $"para la hoja {hoja.Id}"
                        : $"No se obtuvieron revisores relacionados para la hoja {hoja.Id}";

                    _logger.LogInformation(revisoresLogText);
                }
                else
                {
                    _logger.LogInformation($"Se buscarán los revisores anteriores");

                    var siguiente = pasosFlujo.Skip(indexActual + 1)
                        .FirstOrDefault(p => !string.IsNullOrEmpty(p.Valor));

                    revisoresRelacionados = string.IsNullOrEmpty(siguiente.Valor)
                        ? Enumerable.Empty<(string, string)>()
                        : new[] { siguiente };

                    string revisoresConcatenados = string.Join("-", revisoresRelacionados.Select(p => p.Valor));

                    string revisoresLogText = revisoresRelacionados.Count() > 0
                        ? $"Se obtuvieron los siguientes revisores relacionados: {revisoresConcatenados}" +
                                $"para la hoja {hoja.Id}"
                        : $"No se obtuvieron revisores relacionados para la hoja {hoja.Id}";

                    _logger.LogInformation(revisoresLogText);
                }

                foreach (var (nombre, valor) in revisoresRelacionados)
                {
                    _logger.LogInformation($"Búsqueda de revisor relacionado {nombre} para la hoja {hoja.Id}");

                    var revisor = await GetRevisorByName(valor);
                    if (revisor != null)
                    {
                        resultado.Add(revisor);
                        _logger.LogInformation($"Etapa encontrada para la hoja {hoja.Id}: Etapa: {nombre}. Revisor: {valor}");
                    }
                }

                if (!resultado.Any())
                {
                    _logger.LogInformation($"No se encontraron {(buscarAnterior ? "revisores anteriores" : "próximo revisor")} con valor asignado para la hoja {hoja.Id}.");
                }

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error al obtener {(buscarAnterior ? "revisores anteriores" : "próximo revisor")} para la hoja {hoja.Id}. {ex.Message}");
                throw new Exception($"Error al obtener revisores relacionados: {ex.Message}", ex);
            }
        }

        public async Task<SelectListItem?> GetRevisorFromList(string revisoActual, List<Revisores> revisores)
        {
            if (!string.IsNullOrWhiteSpace(revisoActual) &&
                !revisores.Any(item => item.Empleado == revisoActual))
            {
                Revisores reviso = await GetRevisorByName(revisoActual);

                return new SelectListItem
                {
                    Value = reviso.Empleado,
                    Text = reviso.Detalle,
                };
            }

            return null;
        }

        public async Task<bool> IsRevisorAuthorized(Hoja hoja, string revisorActual)
        {
            _logger.LogInformation($"Busqueda de autorizacion de revisor: {revisorActual}" +
                $" para la hoja {hoja.Id}");

            if (hoja == null || string.IsNullOrEmpty(revisorActual))
            {
                _logger.LogError($"El metodo no recibio el revisor o la hoja");
                return false;
            }

            bool result = false;
            Revisores revisor = await GetRevisorByName(revisorActual);

            if (revisor != null)
            {
                _logger.LogInformation($"El revisor {revisorActual} fue encontrado. " +
                    $" con el cargo {revisor.Cargo}");
            }

            var pasosFlujo = new List<string?>
            {
                hoja.Preparo,
                hoja.Reviso,
                hoja.RevisionGerente,
                hoja.EngagementPartner,
                hoja.SocioFirmante,
                hoja.GestorFinal
            };

            switch (revisor.Cargo)
            {
                case 11:
                    result = true;
                    break;
                case 10:
                    result = revisor.Area == hoja.Sector;
                    break;
                default:
                    result = pasosFlujo.Any(p => string.Equals(p, revisorActual, StringComparison.OrdinalIgnoreCase));
                    break;
            }

            return result;
        }
    }
}

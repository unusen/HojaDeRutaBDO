using AutoMapper;
using HojaDeRuta.Helpers;
using HojaDeRuta.Models.DAO;
using HojaDeRuta.Models.DTO;
using HojaDeRuta.Models.Enums;
using HojaDeRuta.Models.ViewModels;

namespace HojaDeRuta.Services.AutoMapper
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Hoja, HojaViewModel>()
                .ForMember(dest => dest.ClienteName,
                    opt => opt.MapFrom((src, dest, destMember, ctx) =>
                        ctx.Items.ContainsKey("Clientes")
                            ? ((List<Clientes>)ctx.Items["Clientes"])
                                .FirstOrDefault(c => c.Id == src.Cliente)?.RazonSocial
                            : null))

                .ForMember(dest => dest.SindicoDetalle,
                    opt => opt.MapFrom((src, dest, destMember, ctx) =>
                        ctx.Items.ContainsKey("Socios")
                            ? ((List<Socios>)ctx.Items["Socios"])
                                .FirstOrDefault(s => s.Socio == src.Sindico)?.Mail
                            : null))

                .ForMember(dest => dest.SocioFirmanteDetalle,
                    opt => opt.MapFrom((src, dest, destMember, ctx) =>
                        ctx.Items.ContainsKey("Socios")
                            ? ((List<Socios>)ctx.Items["Socios"])
                                .FirstOrDefault(s => s.Mail == src.SocioFirmante)
                                ?.Detalle
                            : null));

            CreateMap<HojaViewModel, Hoja>();

            CreateMap<Hoja, HojaFile>()
               .ForMember(dest => dest.Estado,
                opt => opt.MapFrom(src =>
                    EnumHelper.GetDisplayName((Estado)src.Estado)
                ))

                .ForMember(dest => dest.Cliente,
                    opt => opt.MapFrom((src, dest, destMember, ctx) =>
                        ctx.Items.ContainsKey("Clientes")
                            ? ((List<Clientes>)ctx.Items["Clientes"])
                                .FirstOrDefault(c => c.Id == src.Cliente)?.RazonSocial
                            : null))

                .ForMember(dest => dest.Sindico,
                    opt => opt.MapFrom((src, dest, destMember, ctx) =>
                        ctx.Items.ContainsKey("Socios")
                            ? ((List<Socios>)ctx.Items["Socios"])
                                .FirstOrDefault(s => s.Socio == src.Sindico)?.Mail
                            : null))

                .ForMember(dest => dest.SocioFirmante,
                    opt => opt.MapFrom((src, dest, destMember, ctx) =>
                        ctx.Items.ContainsKey("Socios")
                            ? ((List<Socios>)ctx.Items["Socios"])
                                .FirstOrDefault(s => s.Mail == src.SocioFirmante)?.Detalle
                            : null))

                .ForMember(dest => dest.Preparo,
                    opt => opt.MapFrom((src, dest, destMember, ctx) =>
                        ctx.Items.ContainsKey("Revisores")
                            ? ((List<Revisores>)ctx.Items["Revisores"])
                                .FirstOrDefault(s => s.Mail == src.Preparo)?.Detalle
                            : null))

                .ForMember(dest => dest.Reviso,
                    opt => opt.MapFrom((src, dest, destMember, ctx) =>
                        ctx.Items.ContainsKey("Revisores")
                            ? ((List<Revisores>)ctx.Items["Revisores"])
                                .FirstOrDefault(s => s.Mail == src.Reviso)?.Detalle
                            : null))

                .ForMember(dest => dest.RevisionGerente,
                    opt => opt.MapFrom((src, dest, destMember, ctx) =>
                        ctx.Items.ContainsKey("Revisores")
                            ? ((List<Revisores>)ctx.Items["Revisores"])
                                .FirstOrDefault(s => s.Mail == src.RevisionGerente)?.Detalle
                            : null))

                .ForMember(dest => dest.EngagementPartner,
                    opt => opt.MapFrom((src, dest, destMember, ctx) =>
                        ctx.Items.ContainsKey("Revisores")
                            ? ((List<Revisores>)ctx.Items["Revisores"])
                                .FirstOrDefault(s => s.Mail == src.EngagementPartner)?.Detalle
                            : null))

                .ForMember(dest => dest.GestorFinal,
                    opt => opt.MapFrom((src, dest, destMember, ctx) =>
                        ctx.Items.ContainsKey("Revisores")
                            ? ((List<Revisores>)ctx.Items["Revisores"])
                                .FirstOrDefault(s => s.Mail == src.GestorFinal)?.Detalle
                            : null));
        }
    }

}

using AutoMapper;
using HojaDeRuta.Models.DAO;
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
                                    .FirstOrDefault(s => s.Socio == src.SocioFirmante)?.Mail
                                : null));

            CreateMap<HojaViewModel, Hoja>();

            //CreateMap<HojaViewModel, Hoja>()
            //  .ForMember(
            //      dest => dest.FechaDocumento,
            //      opt => opt.MapFrom(src =>
            //          src.FechaDocumento.HasValue
            //          ? src.FechaDocumento.Value.ToString("dd/MM/yyyy")
            //          : null
            //      )
            //  );
        }
    }

}

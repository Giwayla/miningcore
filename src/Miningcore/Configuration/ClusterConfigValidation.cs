/*
MiningCore 2.0
Copyright 2021 MinerNL (Miningcore.com)
*/

using System.IO;
using FluentValidation;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using FluentValidation.Attributes;

namespace Miningcore.Configuration
{
    #region Validators

    public class EmailSenderConfigValidator : AuthenticatedNetworkEndpointConfigValidator<EmailSenderConfig>
    {
        public EmailSenderConfigValidator()
        {
            RuleFor(j => j.FromAddress)
                .NotNull()
                .NotEmpty()
                .WithMessage("EmailSender fromAddress missing or empty");
        }
    }

    public class AdminNotificationsValidator : AbstractValidator<AdminNotifications>
    {
        public AdminNotificationsValidator()
        {
            RuleFor(j => j.EmailAddress)
                .NotNull()
                .NotEmpty()
                .When(x => x.Enabled)
                .WithMessage("Admin notification recipient missing or empty");
        }
    }

    public class NotificationsConfigValidator : AbstractValidator<NotificationsConfig>
    {
        public NotificationsConfigValidator()
        {
            RuleFor(j => j.Email)
                .NotNull()
                .When(x => x.Enabled)
                .WithMessage("You must configure at least one notifications provider when notifications are enabled");
        }
    }

    public class NetworkEndpointConfigValidator<T> : AbstractValidator<T>
        where T : NetworkEndpointConfig
    {
        public NetworkEndpointConfigValidator()
        {
            RuleFor(j => j.Host)
                .NotNull()
                .NotEmpty()
                .WithMessage("Host missing or empty");

            RuleFor(j => j.Port)
                .GreaterThan(0)
                .WithMessage("Invalid port number '{PropertyValue}'");
        }
    }

    public class AuthenticatedNetworkEndpointConfigValidator<T> : NetworkEndpointConfigValidator<T>
        where T : AuthenticatedNetworkEndpointConfig
    {
    }

    public class PoolEndpointValidator : AbstractValidator<PoolEndpoint>
    {
        public PoolEndpointValidator()
        {
            RuleFor(j => j.Difficulty)
                .GreaterThan(0)
                .WithMessage("Pool Endpoint: Difficulty missing or invalid");

            RuleFor(j => j.TlsPfxFile)
                .NotNull()
                .NotEmpty()
                .When(j => j.Tls)
                .WithMessage("Pool Endpoint: Tls enabled but neither TlsPemFile nor TlsPfxFile specified");

            RuleFor(j => j.TlsPfxFile)
                .Must(File.Exists)
                .When(j => j.Tls && j.TlsPfx == null)
                .WithMessage(j => $"Pool Endpoint: {j.TlsPfxFile} does not exist");

            RuleFor(j => j.TlsPfxFile)
                .Must(j =>
                {
                    try
                    {
                        var tlsCert = new X509Certificate2(j);
                        return tlsCert.HasPrivateKey;
                    }
                    catch
                    {
                        return false;
                    }
                })
                .When(j => j.Tls && j.TlsPfx == null)
                .WithMessage(j => $"Pool Endpoint: {j.TlsPfxFile} is not valid or does not include the private key and cannot be used");

            RuleFor(j => j.TlsPfx)
                .Must(j => j.HasPrivateKey)
                .When(j => j.Tls && j.TlsPfx != null)
                .WithMessage(j => $"Pool Endpoint: {j.TlsPfx} is not valid or does not include the private key and cannot be used");

            RuleFor(j => j.VarDiff)
                .SetValidator(new VarDiffConfigValidator())
                .When(x => x.VarDiff != null);
        }
    }

    public class ApiConfigValidator : AbstractValidator<ApiConfig>
    {
        public ApiConfigValidator()
        {
            RuleFor(j => j.ListenAddress)
                .NotNull()
                .NotEmpty()
                .WithMessage("API: listenAddress missing or empty");

            RuleFor(j => j.Port)
                .GreaterThan(0)
                .WithMessage("API: Invalid port number '{PropertyValue}'");
        }
    }

    public class VarDiffConfigValidator : AbstractValidator<VarDiffConfig>
    {
        public VarDiffConfigValidator()
        {
            RuleFor(j => j.MaxDiff)
                .GreaterThanOrEqualTo(x => x.MinDiff)
                .When(x => x.MaxDiff.HasValue)
                .WithMessage("VarDiff: max value must be greater or equal min value");

            RuleFor(j => j.VariancePercent)
                .InclusiveBetween(1, 100)
                .WithMessage("VarDiff: variancePercent must be a percentage betwen 1 and 100");

            RuleFor(j => j.TargetTime)
                .GreaterThan(0)
                .WithMessage("VarDiff: targetTime invalid");

            RuleFor(j => j.RetargetTime)
                .GreaterThan(0)
                .WithMessage("VarDiff: retargetTime invalid");
        }
    }

    public class PoolConfigValidator : AbstractValidator<PoolConfig>
    {
        public PoolConfigValidator()
        {
            RuleFor(j => j.Id)
                .NotNull()
                .NotEmpty()
                .WithMessage("Pool: id missing or empty");

            RuleFor(j => j.Coin)
                .NotNull()
                .WithMessage("Pool: Coin config missing or empty");

            RuleFor(j => j.Ports)
                .NotNull()
                .NotEmpty()
                .When(j => j.EnableInternalStratum == true)
                .WithMessage("Pool: Stratum port config missing or empty");

            RuleFor(j => j.Ports)
                .Must((pc, ports, ctx) =>
                {
                    if(ports?.Keys.Any(port => port < 0) == true)
                    {
                        ctx.MessageFormatter.AppendArgument("port", ports.Keys.First(port => port < 0));
                        return false;
                    }

                    return true;
                })
                .WithMessage("Pool: Invalid stratum port number {port}");

            RuleForEach(j => j.Ports.Values)
                .SetValidator(x => new PoolEndpointValidator())
                .When(x => x.Ports != null);

            RuleFor(j => j.Address)
                .NotNull()
                .NotEmpty()
                .WithMessage("Pool: Wallet address missing or empty");

            RuleFor(j => j.Daemons)
                .NotNull()
                .NotEmpty()
                .WithMessage("Pool: Daemons missing or empty");

            RuleForEach(j => j.Daemons)
                .SetValidator(new AuthenticatedNetworkEndpointConfigValidator<DaemonEndpointConfig>());
        }
    }

    public class ClusterConfigValidator : AbstractValidator<ClusterConfig>
    {
        public ClusterConfigValidator()
        {
            RuleFor(j => j.PaymentProcessing)
                .NotNull();

            RuleFor(j => j.Persistence)
                .NotNull();

            RuleFor(j => j.Pools)
                .NotNull()
                .NotEmpty();

            // ensure pool ids are unique
            RuleFor(j => j.Pools)
                .Must((pc, pools, ctx) =>
                {
                    var ids = pools
                        .GroupBy(x => x.Id)
                        .ToArray();

                    if(ids.Any(id => id.Count() > 1))
                    {
                        ctx.MessageFormatter.AppendArgument("poolId", ids.First(id => id.Count() > 1).Key);
                        return false;
                    }

                    return true;
                })
                .WithMessage("Duplicate pool id '{poolId}'");

            // ensure stratum ports are not assigned multiple times
            RuleFor(j => j.Pools)
                .Must((pc, pools, ctx) =>
                {
                    var ports = pools.Where(x => x.Ports?.Any() == true).SelectMany(x => x.Ports.Select(y => y.Key))
                        .GroupBy(x => x)
                        .ToArray();

                    foreach(var port in ports)
                    {
                        if(port.Count() > 1)
                        {
                            ctx.MessageFormatter.AppendArgument("port", port.Key);
                            return false;
                        }
                    }

                    return true;
                })
                .WithMessage("Stratum port {port} assigned multiple times");

            RuleForEach(j => j.Pools)
                .SetValidator(new PoolConfigValidator());
        }
    }

    #endregion // Validators

    public partial class ClusterLoggingConfig
    {
    }

    [Validator(typeof(VarDiffConfigValidator))]
    public partial class VarDiffConfig
    {
    }

    public partial class PoolShareBasedBanningConfig
    {
    }

    public partial class PoolPaymentProcessingConfig
    {
    }

    public partial class ClusterPaymentProcessingConfig
    {
    }

    public partial class PersistenceConfig
    {
    }

    [Validator(typeof(NetworkEndpointConfigValidator<NetworkEndpointConfig>))]
    public partial class NetworkEndpointConfig
    {
    }

    [Validator(typeof(AuthenticatedNetworkEndpointConfigValidator<AuthenticatedNetworkEndpointConfig>))]
    public partial class AuthenticatedNetworkEndpointConfig
    {
    }

    [Validator(typeof(EmailSenderConfigValidator))]
    public partial class EmailSenderConfig
    {
    }

    [Validator(typeof(AdminNotificationsValidator))]
    public partial class AdminNotifications
    {
    }

    [Validator(typeof(NotificationsConfigValidator))]
    public partial class NotificationsConfig
    {
    }

    [Validator(typeof(ApiConfigValidator))]
    public partial class ApiConfig
    {
    }

    [Validator(typeof(PoolConfigValidator))]
    public partial class PoolConfig
    {
    }

    [Validator(typeof(ClusterConfigValidator))]
    public partial class ClusterConfig
    {
        public void Validate()
        {
            var validator = new ClusterConfigValidator();
            var result = validator.Validate(this);

            if(!result.IsValid)
                throw new ValidationException(result.Errors);
        }
    }
}

using Diten.AuthService.ServiceClientCredentialGenerator;

// MOD-0033-FU02 §F.2 — standard input is never read: the generator accepts no secret from outside.
return GeneratorCommand.Run(args, Console.Out, Console.Error);

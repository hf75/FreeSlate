import { dotnet } from './_framework/dotnet.js'

const runtime = await dotnet.create();
// The page's address, which the program fetches the shelf's fonts from (Program.Address).
runtime.setModuleImports('main.js', { page: { address: () => document.baseURI } });
await runtime.runMain();

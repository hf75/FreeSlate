import { dotnet } from './_framework/dotnet.js'

const runtime = await dotnet.create();
await runtime.runMain();

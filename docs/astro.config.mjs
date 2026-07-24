// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import starlightFSharpDoc from 'starlight-fsharp-oracle';
import { fileURLToPath } from 'node:url';
import { resolve, dirname } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));

// https://astro.build/config
export default defineConfig({
    integrations: [
        starlight({
            title: 'My Docs',
            social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/withastro/starlight' }],
            plugins: [
                starlightFSharpDoc({
                    assemblies: [
                        resolve(__dirname, '../tests/Reference/bin/Debug/net10.0/Reference.dll'),
                    ],
                    output: 'api',
                    sidebar: { label: 'API Reference' },
                }),
            ],
            sidebar: [
                {
                    label: 'Guides',
                    items: [
                        // Each item here is one entry in the navigation menu.
                        { label: 'Example Guide', slug: 'guides/example' },
                    ],
                },
                {
                    label: 'Reference',
                    autogenerate: { directory: 'reference' },
                }
            ],
        }),
    ],
});



    // dotnet run --project packages/FSharp.Oracle/ \
    //     --output-base \
    //     api \
    //     /home/mmangel/Workspaces/Github/MangelMaxime/starlight-fsharp-doc/main/reproduction/src/Fable.Ink/bin/Release/netstandard2.1/publish/Fable.Ink.dll \
    //     /home/mmangel/Workspaces/Github/MangelMaxime/starlight-fsharp-doc/main/reproduction/src/Fable.Parchment/bin/Release/netstandard2.1/publish/Fable.Parchment.dll \
    //     /home/mmangel/Workspaces/Github/MangelMaxime/starlight-fsharp-doc/main/reproduction/src/Fable.Nib/bin/Release/netstandard2.1/publish/Fable.Nib.dll \
    //     /home/mmangel/Workspaces/Github/MangelMaxime/starlight-fsharp-doc/main/reproduction/src/Fable.Quill/bin/Release/netstandard2.1/publish/Fable.Quill.dll \
    //     /home/mmangel/Workspaces/Github/MangelMaxime/starlight-fsharp-doc/main/reproduction/src/Fable.Nib.Snapshot/bin/Release/net10.0/publish/Fable.Nib.Snapshot.dll \
    //     /home/mmangel/Workspaces/Github/MangelMaxime/starlight-fsharp-doc/main/reproduction/src/Fable.Nib.Dom/bin/Release/netstandard2.1/publish/Fable.Nib.Dom.dll

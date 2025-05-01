// Main entry point for the Avalonia WASM app

import { dotnet } from './_framework/dotnet.js';

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

const getAssemblyExports = async (assemblyName) => {
    const exports = await dotnet.getAssemblyExports(assemblyName);
    return exports;
};

async function main() {
    try {
        const config = {
            mainAssemblyName: 'PokerGame.Avalonia',
            resources: {
                run: {
                    fetch: (url, init) => fetch(url, init)
                },
                app: {
                    init: () => { }
                }
            }
        };

        await dotnet.run(config);
        console.log("Avalonia WASM application initialized successfully");
    } catch (error) {
        console.error(`Error initializing Avalonia WASM app: ${error}`);
    }
}

main().catch(console.error);
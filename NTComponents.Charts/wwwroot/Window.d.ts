export {};

declare global {
    interface Window {
        NTComponents: {
            getColorValueFromEnumName(colorName: string): string | null | undefined;
        };
    }
}

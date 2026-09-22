using RepoLens.Analysis.TypeScript;

namespace RepoLens.AnalysisTests.TypeScript;

public class TsSymbolExtractorTests
{
    private readonly TsSymbolExtractor _extractor = new();

    [Fact]
    public void Extract_WithVariousTypeScriptDeclarations_ExtractsAllSymbols()
    {
        // Arrange
        var tsCode = """
            export interface IUserDto {
                id: string;
            }

            export class UserService {
                login() {}
            }

            export type ApiResponse<T> = { data: T };

            export enum Role {
                Admin = "admin"
            }

            export async function fetchUsers() {
                return [];
            }

            export const calculateTotal = (a: number, b: number) => a + b;
            """;

        // Act
        var symbols = _extractor.Extract(tsCode, "user.service.ts");

        // Assert
        Assert.Contains(symbols, s => s.Kind == TsSymbolKind.Interface && s.Name == "IUserDto" && s.IsExported);
        Assert.Contains(symbols, s => s.Kind == TsSymbolKind.Class && s.Name == "UserService" && s.IsExported);
        Assert.Contains(symbols, s => s.Kind == TsSymbolKind.Type && s.Name == "ApiResponse" && s.IsExported);
        Assert.Contains(symbols, s => s.Kind == TsSymbolKind.Enum && s.Name == "Role" && s.IsExported);
        Assert.Contains(symbols, s => s.Kind == TsSymbolKind.Function && s.Name == "fetchUsers" && s.IsExported);
        Assert.Contains(symbols, s => s.Kind == TsSymbolKind.Function && s.Name == "calculateTotal" && s.IsExported);
    }

    [Fact]
    public void Extract_WhenSourceCodeHasNoRecognizedDeclarations_ReturnsEmptyList()
    {
        // Arrange (Negative case)
        var tsCode = """
            // This file only contains comments and raw expressions
            import React from "react";
            console.log("Hello, world!");
            /* multi-line comment
               interface FakeInsideComment
            */
            """;

        // Act
        var symbols = _extractor.Extract(tsCode, "noop.ts");

        // Assert
        Assert.Empty(symbols);
    }
}

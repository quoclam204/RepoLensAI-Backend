using RepoLens.Analysis.TypeScript;

namespace RepoLens.AnalysisTests.TypeScript;

public class TsSymbolExtractorTests
{
    private readonly TsSymbolExtractor _extractor = new();

    [Fact]
    public void ExtractWithImports_WithTypesAndOrdersFixture_ExtractsSymbolsAndImportsAccurately()
    {
        // Arrange
        var typesTs = """
            export interface Order {
                id: number;
            }
            """;

        var ordersTs = """
            import { Order } from "./types";

            export function getOrders(): Order[] {
                return [];
            }
            """;

        // Act
        var typesResult = _extractor.ExtractWithImports(typesTs, "types.ts");
        var ordersResult = _extractor.ExtractWithImports(ordersTs, "orders.ts");

        // Assert - types.ts
        Assert.Single(typesResult.Symbols);
        var orderInterface = typesResult.Symbols[0];
        Assert.Equal("Order", orderInterface.Name);
        Assert.Equal(TsSymbolKind.Interface, orderInterface.Kind);
        Assert.True(orderInterface.IsExported);
        Assert.Empty(typesResult.Imports);

        // Assert - orders.ts
        Assert.Single(ordersResult.Symbols);
        var getOrdersFn = ordersResult.Symbols[0];
        Assert.Equal("getOrders", getOrdersFn.Name);
        Assert.Equal(TsSymbolKind.Function, getOrdersFn.Kind);
        Assert.True(getOrdersFn.IsExported);

        Assert.Single(ordersResult.Imports);
        var imp = ordersResult.Imports[0];
        Assert.Equal("./types", imp.ModulePath);
        Assert.Equal("orders.ts", imp.Location.FilePath);
        Assert.True(imp.Location.StartLine > 0);
        Assert.Contains("./types", imp.Snippet);
    }

    [Fact]
    public void Extract_WithReactComponent_DistinguishesComponentFromStandardFunction()
    {
        // Arrange
        var tsxCode = """
            export function OrderList() {
                return <div>Orders</div>;
            }

            export function calculateTotal(items: any[]) {
                return items.length;
            }
            """;

        var tsCode = """
            export function OrderList() {
                return "Orders";
            }
            """;

        // Act
        var tsxSymbols = _extractor.Extract(tsxCode, "OrderList.tsx");
        var tsSymbols = _extractor.Extract(tsCode, "OrderList.ts");

        // Assert - In .tsx file, PascalCase function is recognized as Component
        var component = tsxSymbols.Single(s => s.Name == "OrderList");
        Assert.Equal(TsSymbolKind.Component, component.Kind);

        // In .tsx file, camelCase function is NOT recognized as Component
        var helperFn = tsxSymbols.Single(s => s.Name == "calculateTotal");
        Assert.Equal(TsSymbolKind.Function, helperFn.Kind);

        // In plain .ts file, PascalCase function is NOT Component (requires .tsx or .jsx)
        var nonTsxComponent = tsSymbols.Single(s => s.Name == "OrderList");
        Assert.Equal(TsSymbolKind.Function, nonTsxComponent.Kind);
    }

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

    [Fact]
    public void ExtractAll_WithRoutesAndApiCalls_ExtractsRoutesAndCallsAccurately()
    {
        // Arrange
        var tsxCode = """
            import React from "react";
            import { Route } from "react-router-dom";
            import axios from "axios";

            export function App() {
                return (
                    <div>
                        <Route path="/orders" element={<OrderList />} />
                        <Route path="/users" component={UserList} />
                    </div>
                );
            }

            export async function loadData() {
                const orders = await axios.get("/api/orders");
                const res = await fetch("/api/users");
                await axios.post("/api/orders/create");
            }
            """;

        // Act
        var (symbols, imports, routes, apiCalls) = _extractor.ExtractAll(tsxCode, "App.tsx");

        // Assert - Routes
        Assert.Equal(2, routes.Count);
        Assert.Contains(routes, r => r.RouteTemplate == "/orders" && r.HandlerOrComponent == "OrderList" && r.HttpMethod == "GET");
        Assert.Contains(routes, r => r.RouteTemplate == "/users" && r.HandlerOrComponent == "UserList" && r.HttpMethod == "GET");

        // Assert - API calls
        Assert.Equal(3, apiCalls.Count);
        Assert.Contains(apiCalls, c => c.HttpMethod == "GET" && c.EndpointUrl == "/api/orders");
        Assert.Contains(apiCalls, c => c.HttpMethod == "GET" && c.EndpointUrl == "/api/users");
        Assert.Contains(apiCalls, c => c.HttpMethod == "POST" && c.EndpointUrl == "/api/orders/create");
    }
}

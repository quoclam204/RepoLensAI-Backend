import express from "express";
import { UserService } from "./userService";
import { formatUser } from "./userService";

const app = express();
const userService = new UserService();

app.get("/api/users", (req, res) => {
    const user = userService.getUser("1");
    res.json(user ? formatUser(user) : null);
});

export async function checkHealth(): Promise<string> {
    const response = await fetch("/api/health");
    return response.ok ? "healthy" : "degraded";
}

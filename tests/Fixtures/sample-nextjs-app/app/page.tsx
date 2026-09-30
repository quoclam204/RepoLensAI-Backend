import React, { useEffect, useState } from "react";
import axios from "axios";

export interface UserProfile {
    id: string;
    name: string;
}

export default function HomePage() {
    const [users, setUsers] = useState<UserProfile[]>([]);

    useEffect(() => {
        axios.get("/api/users").then(res => setUsers(res.data));
    }, []);

    return <div>Hello Next.js</div>;
}

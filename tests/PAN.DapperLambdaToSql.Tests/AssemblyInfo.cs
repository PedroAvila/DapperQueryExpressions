// DapperHelper.Dialect es estado estático global. Si las colecciones corrieran en
// paralelo, una prueba que fuerza el dialecto podría filtrarse a otra que está
// verificando la detección automática. La batería es pequeña; serializarla sale gratis.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

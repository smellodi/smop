# SMELLODI odor printer (SMOP): Machine Learning communication module

The ML module provide Communicator class to be utilized by the main SMOP app. The communicator 
instantiates a server that delegates ML implementation to either an external module that could be 
accessed via TCP connection or via file IO operation (currently, this is a Matlab-based executable
ML app), or a module that implements the search-based ML algorithm locally. The latter one is 
recommended and used in SMOP main app by default.

## Testing

Use `test.ml` console app to test the module
